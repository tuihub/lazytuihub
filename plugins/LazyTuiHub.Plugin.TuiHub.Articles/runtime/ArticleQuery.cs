namespace LazyTuiHub.Articles;

public sealed record ArticleQuery(
    string? Source = null,
    bool? Read = null,
    long BeforeSequence = long.MaxValue,
    long Count = long.MaxValue
);
