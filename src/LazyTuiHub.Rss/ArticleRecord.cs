using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record ArticleRecord(long Sequence, string Id, ArticleDraft Article, bool IsRead)
{
    public DataReference Reference => new("rss.article", $"{Article.FeedId}/{Id}");
}
