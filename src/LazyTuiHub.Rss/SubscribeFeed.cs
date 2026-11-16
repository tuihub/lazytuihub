using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record SubscribeFeed(string Id, Uri Url, string DisplayName, string RequestId);
