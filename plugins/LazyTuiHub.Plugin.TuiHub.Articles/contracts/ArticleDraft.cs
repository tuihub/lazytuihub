namespace LazyTuiHub.Api.Articles;

public sealed record ArticleDraft(
    string Source,
    string EntryId,
    string Title,
    string Url,
    string Content,
    DateTimeOffset? PublishedAt
);
