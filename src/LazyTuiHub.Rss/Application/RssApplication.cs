using System.Globalization;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Hosting;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub.Rss.Application;

public sealed class RssApplication(
    RssOperations workspace,
    IWorkflowExecutor workflows,
    IDataProtectionProvider protection,
    ILogger<RssApplication> logger
) : IRssApi
{
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
            var feeds = await workflows.CollectAsync<FeedRecord>(
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
            var size = PageTokens.PageSize(request.PageSize);
            var after = tokens.Decode(FeedCollection, request.PageToken, string.Empty);
            var feeds = await workflows.CollectAsync<FeedRecord>(
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

    private async Task<FeedRecord> FindFeedAsync(string id, CancellationToken cancellationToken)
    {
        var feeds = await workflows.CollectAsync<FeedRecord>("rss.feeds", new FeedSelection(id), cancellationToken);
        return feeds.SingleOrDefault()
            ?? throw ApiErrors.Create(StatusCode.NotFound, "FEED_NOT_FOUND", "The subscription does not exist.");
    }

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

    private Task<T> RunAsync<T>(Func<Task<T>> run) => ApiErrors.RunAsync(run, logger, error => error switch
            {
                RssConflictException { Reason: "FEED_EXISTS" } conflict => ApiErrors.Create(
                    StatusCode.AlreadyExists,
                    conflict.Reason,
                    conflict.Message
                ),
                RssConflictException conflict => ApiErrors.Create(
                    StatusCode.InvalidArgument,
                    conflict.Reason,
                    conflict.Message
                ),
                _ => null,
            });
}
