namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed record FetchDetail(string Url, long Bytes, DateTimeOffset CompletedAt);
