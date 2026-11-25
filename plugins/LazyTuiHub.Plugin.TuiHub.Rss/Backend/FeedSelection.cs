using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed record FeedSelection(string? FeedId = null, string AfterId = "", long Count = long.MaxValue);
