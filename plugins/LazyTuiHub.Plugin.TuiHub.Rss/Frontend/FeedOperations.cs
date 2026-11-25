using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Interaction;
using LazyTuiHub.Plugin.TuiHub.Rss.Api;

namespace LazyTuiHub.Plugin.TuiHub.Rss.Frontend;

public class FeedOperations(IRssApi api)
{
    [Capability("List RSS subscriptions.")]
    public Task<ListFeedsResponse> Feeds(CancellationToken token, string pageToken = "") =>
        api.ListFeedsAsync(new() { PageToken = pageToken }, token);

    [Capability("Subscribe to a feed URL.")]
    public Task<Feed> Add(
        string feedUrl,
        CancellationToken token,
        string title = "",
        string feedId = "",
        string? requestId = null
    ) =>
        api.CreateFeedAsync(
            new()
            {
                Feed = new() { Uri = feedUrl, DisplayName = title },
                FeedId = feedId,
                RequestId = requestId ?? Guid.NewGuid().ToString("D"),
            },
            token
        );

    [Capability("Open a subscription by its resource name.")]
    public Task<Feed> Get(string name, CancellationToken token) => api.GetFeedAsync(new() { Name = name }, token);

    [Capability("Fetch new articles from a subscription.")]
    public Task<RefreshFeedResponse> Refresh(string name, CancellationToken token) =>
        api.RefreshFeedAsync(new() { Name = name }, token);
}
