using System.Text.Json;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Client;
using LazyTuiHub.Plugin.TuiHub.Rss.Api;

namespace LazyTuiHub.Plugin.TuiHub.Rss.Frontend;

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

    private static string FeedSegment(string name)
    {
        return name.Split('/') is ["feeds", var feed]
            ? feed
            : throw new ArgumentException("Expected feeds/{feed}.", nameof(name));
    }
}
