using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Rss;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub.Application;

public sealed class RssApplication(
    RssWorkspace workspace,
    IDataProtectionProvider protection,
    ILogger<RssApplication> logger
) : IRssApi
{
    private const int DefaultPageSize = 50;
    private const string FeedCollection = "feeds";
    private const int RequestUuidVersion = 4;
    private const int MinimumRequestUuidVariant = 8;
    private const int MaximumRequestUuidVariant = 11;
    private readonly PageTokens tokens = new(protection);

    public Task<Feed> CreateFeedAsync(CreateFeedRequest request, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            if (
                request.Feed is null
                || !Uri.TryCreate(request.Feed.Uri, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
            )
                throw ApiErrors.Create(
                    StatusCode.InvalidArgument,
                    "INVALID_FEED_URI",
                    "Provide an absolute HTTP or HTTPS feed URI."
                );
            if (
                !string.IsNullOrEmpty(request.FeedId)
                && (
                    !Guid.TryParseExact(request.FeedId, "D", out var id)
                    || !request.FeedId.Equals(id.ToString("D"), StringComparison.Ordinal)
                )
            )
                throw ApiErrors.Create(
                    StatusCode.InvalidArgument,
                    "INVALID_FEED_ID",
                    "The feed ID must be a lowercase, hyphenated UUID."
                );
            var requestId = NormalizeRequestId(request.RequestId);
            var feeds = await workspace.RunAsync<FeedRecord>(
                "rss.subscribe",
                new SubscribeFeed(request.FeedId, uri, request.Feed.DisplayName, requestId),
                cancellationToken
            );
            return ToFeed(feeds.Single());
        });

    public Task<ListFeedsResponse> ListFeedsAsync(
        ListFeedsRequest request,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(async () =>
        {
            var size = PageSize(request.PageSize);
            var after = tokens.Decode(FeedCollection, request.PageToken, string.Empty);
            var feeds = await workspace.RunAsync<FeedRecord>(
                "rss.feeds",
                new FeedSelection(AfterId: after, Count: (long)size + 1),
                cancellationToken
            );
            var response = new ListFeedsResponse();
            response.Feeds.AddRange(feeds.Take(size).Select(ToFeed));
            if (feeds.Count > size)
                response.NextPageToken = tokens.Encode(FeedCollection, feeds[size - 1].Id);
            return response;
        });

    public Task<Feed> GetFeedAsync(GetFeedRequest request, CancellationToken cancellationToken = default) =>
        RunAsync(async () => ToFeed(await FindFeedAsync(FeedId(request.Name), cancellationToken)));

    public Task<RefreshFeedResponse> RefreshFeedAsync(
        RefreshFeedRequest request,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(async () =>
        {
            var id = FeedId(request.Name);
            await FindFeedAsync(id, cancellationToken);
            return new RefreshFeedResponse
            {
                ProcessedArticleCount = await workspace.RefreshAsync(new(id), cancellationToken),
            };
        });

    public Task<ListArticlesResponse> ListArticlesAsync(
        ListArticlesRequest request,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(async () =>
        {
            var id = FeedId(request.Parent);
            if (id is not "-")
                await FindFeedAsync(id, cancellationToken);
            var size = PageSize(request.PageSize);
            var read = request.Filter switch
            {
                "" => (bool?)null,
                "read = true" => true,
                "read = false" => false,
                _ => throw ApiErrors.Create(
                    StatusCode.InvalidArgument,
                    "INVALID_FILTER",
                    "Supported filters are read = true and read = false."
                ),
            };
            var scope = $"articles:{request.Parent}:{request.Filter}";
            var before = long.Parse(
                tokens.Decode(scope, request.PageToken, long.MaxValue.ToString(CultureInfo.InvariantCulture)),
                CultureInfo.InvariantCulture
            );
            var articles = await workspace.RunAsync<ArticleRecord>(
                "rss.articles",
                new ArticleQuery(id is "-" ? null : id, read, before, (long)size + 1),
                cancellationToken
            );
            var response = new ListArticlesResponse();
            response.Articles.AddRange(articles.Take(size).Select(ToArticle));
            if (articles.Count > size)
                response.NextPageToken = tokens.Encode(
                    scope,
                    articles[size - 1].Sequence.ToString(CultureInfo.InvariantCulture)
                );
            return response;
        });

    public Task<Article> GetArticleAsync(GetArticleRequest request, CancellationToken cancellationToken = default) =>
        ReadArticleAsync("rss.article", request.Name, cancellationToken);

    public Task<Article> MarkArticleReadAsync(
        MarkArticleReadRequest request,
        CancellationToken cancellationToken = default
    ) => ReadArticleAsync("rss.mark-read", request.Name, cancellationToken);

    private Task<Article> ReadArticleAsync(string pipeline, string name, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            if (name.Split('/') is not [FeedCollection, { Length: > 0 } feed, "articles", { Length: > 0 } article])
                throw ApiErrors.Create(
                    StatusCode.InvalidArgument,
                    "INVALID_ARTICLE_NAME",
                    "Use an article name of the form feeds/{feed}/articles/{article}."
                );
            var articles = await workspace.RunAsync<ArticleRecord>(
                pipeline,
                new ArticleSelection(feed, article),
                cancellationToken
            );
            return ToArticle(articles.Single());
        });

    private async Task<FeedRecord> FindFeedAsync(string id, CancellationToken cancellationToken)
    {
        var feeds = await workspace.RunAsync<FeedRecord>("rss.feeds", new FeedSelection(id), cancellationToken);
        return feeds.SingleOrDefault()
            ?? throw ApiErrors.Create(StatusCode.NotFound, "FEED_NOT_FOUND", "The subscription does not exist.");
    }

    private static int PageSize(int requested) =>
        requested switch
        {
            < 0 => throw ApiErrors.Create(
                StatusCode.InvalidArgument,
                "INVALID_PAGE_SIZE",
                "Page size cannot be negative."
            ),
            0 => DefaultPageSize,
            _ => requested,
        };

    private static string FeedId(string name)
    {
        if (name.Split('/') is not [FeedCollection, { Length: > 0 } id])
            throw ApiErrors.Create(
                StatusCode.InvalidArgument,
                "INVALID_FEED_NAME",
                "Use a subscription name of the form feeds/{feed}."
            );
        return id;
    }

    private static string NormalizeRequestId(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (
            !Guid.TryParseExact(value, "D", out var id)
            || id.Version != RequestUuidVersion
            || id.Variant is < MinimumRequestUuidVariant or > MaximumRequestUuidVariant
        )
            throw ApiErrors.Create(
                StatusCode.InvalidArgument,
                "INVALID_REQUEST_ID",
                "The request ID must be a UUID version 4."
            );
        return id.ToString("D");
    }

    private static Feed ToFeed(FeedRecord feed) =>
        new()
        {
            Name = $"feeds/{feed.Id}",
            Uri = feed.Url,
            DisplayName = feed.DisplayName,
        };

    private Task<T> RunAsync<T>(Func<Task<T>> run) => ApiErrors.RunAsync(run, logger);

    private static Article ToArticle(ArticleRecord record) =>
        new()
        {
            Name = $"feeds/{record.Article.FeedId}/articles/{record.Id}",
            Title = record.Article.Title,
            Uri = record.Article.Url,
            Content = record.Article.Content,
            Read = record.IsRead,
            PublishTime = record.Article.PublishedAt is { } published ? Timestamp.FromDateTimeOffset(published) : null,
        };
}
