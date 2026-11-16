using System.Text.Json;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Rss.V1;

namespace LazyTuiHub.Client;

public partial class RssHttpClient : IRssApi
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
        settings.Converters.Add(new ProtobufJsonConverter());

    public Task<Feed> CreateFeedAsync(CreateFeedRequest request, CancellationToken cancellationToken = default) =>
        CreateFeedAsync(request.Feed, request.FeedId, request.RequestId, cancellationToken);

    public Task<ListFeedsResponse> ListFeedsAsync(
        ListFeedsRequest request,
        CancellationToken cancellationToken = default
    ) => ListFeedsAsync(request.PageSize, request.PageToken, cancellationToken);

    public Task<Feed> GetFeedAsync(GetFeedRequest request, CancellationToken cancellationToken = default) =>
        GetFeedAsync(FeedSegment(request.Name), cancellationToken);

    public Task<RefreshFeedResponse> RefreshFeedAsync(
        RefreshFeedRequest request,
        CancellationToken cancellationToken = default
    ) => RefreshFeedAsync(FeedSegment(request.Name), request, cancellationToken);

    public Task<ListArticlesResponse> ListArticlesAsync(
        ListArticlesRequest request,
        CancellationToken cancellationToken = default
    ) =>
        ListArticlesAsync(
            FeedSegment(request.Parent),
            request.PageSize,
            request.PageToken,
            request.Filter,
            cancellationToken
        );

    public Task<Article> GetArticleAsync(GetArticleRequest request, CancellationToken cancellationToken = default)
    {
        var (feed, article) = ArticleSegments(request.Name);
        return GetArticleAsync(feed, article, cancellationToken);
    }

    public Task<Article> MarkArticleReadAsync(
        MarkArticleReadRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var (feed, article) = ArticleSegments(request.Name);
        return MarkArticleReadAsync(feed, article, request, cancellationToken);
    }

    private static string FeedSegment(string name)
    {
        return name.Split('/') is ["feeds", var feed]
            ? feed
            : throw new ArgumentException("Expected feeds/{feed}.", nameof(name));
    }

    private static (string Feed, string Article) ArticleSegments(string name)
    {
        return name.Split('/') is ["feeds", var feed, "articles", var article]
            ? (feed, article)
            : throw new ArgumentException("Expected feeds/{feed}/articles/{article}.", nameof(name));
    }
}
