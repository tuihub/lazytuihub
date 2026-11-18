using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Rss.V1;
using RssContract = LazyTuiHub.Api.Rss.V1.RssService;

namespace LazyTuiHub.Rss.Transport;

internal sealed class RssService(IRssApi application) : RssContract.RssServiceBase
{
    public override Task<Feed> CreateFeed(CreateFeedRequest request, ServerCallContext context) =>
        application.CreateFeedAsync(request, context.CancellationToken);

    public override Task<ListFeedsResponse> ListFeeds(ListFeedsRequest request, ServerCallContext context) =>
        application.ListFeedsAsync(request, context.CancellationToken);

    public override Task<Feed> GetFeed(GetFeedRequest request, ServerCallContext context) =>
        application.GetFeedAsync(request, context.CancellationToken);

    public override Task<RefreshFeedResponse> RefreshFeed(RefreshFeedRequest request, ServerCallContext context) =>
        application.RefreshFeedAsync(request, context.CancellationToken);
}
