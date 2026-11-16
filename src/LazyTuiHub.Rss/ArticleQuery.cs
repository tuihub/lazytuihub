using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record ArticleQuery(
    string? FeedId = null,
    bool? Read = null,
    long BeforeSequence = long.MaxValue,
    long Count = long.MaxValue
);
