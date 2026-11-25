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
}
