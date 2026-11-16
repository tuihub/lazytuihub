using LazyTuiHub.Api.Rss.V1;

namespace LazyTuiHub.Api;

public interface IRssApi
{
    Task<Feed> CreateFeedAsync(CreateFeedRequest request, CancellationToken cancellationToken = default);
    Task<ListFeedsResponse> ListFeedsAsync(ListFeedsRequest request, CancellationToken cancellationToken = default);
    Task<Feed> GetFeedAsync(GetFeedRequest request, CancellationToken cancellationToken = default);
    Task<RefreshFeedResponse> RefreshFeedAsync(
        RefreshFeedRequest request,
        CancellationToken cancellationToken = default
    );
    Task<ListArticlesResponse> ListArticlesAsync(
        ListArticlesRequest request,
        CancellationToken cancellationToken = default
    );
    Task<Article> GetArticleAsync(GetArticleRequest request, CancellationToken cancellationToken = default);
    Task<Article> MarkArticleReadAsync(MarkArticleReadRequest request, CancellationToken cancellationToken = default);
}
