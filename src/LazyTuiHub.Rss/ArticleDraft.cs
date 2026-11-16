using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record ArticleDraft(
    string FeedId,
    string EntryId,
    string Title,
    string Url,
    string Content,
    DateTimeOffset? PublishedAt
);
