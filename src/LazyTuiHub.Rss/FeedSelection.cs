using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record FeedSelection(string? FeedId = null, string AfterId = "", long Count = long.MaxValue);
