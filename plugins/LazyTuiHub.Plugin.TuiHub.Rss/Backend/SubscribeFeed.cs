using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed record SubscribeFeed(string Id, Uri Url, string DisplayName, string RequestId);
