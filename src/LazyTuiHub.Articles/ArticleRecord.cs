using LazyTuiHub.Api.Articles;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Articles;

public sealed record ArticleRecord(
    long Sequence,
    string Id,
    ArticleDraft Article,
    bool IsRead,
    string Account = ResourceAccess.LocalAccount
)
{
    public DataReference Reference => new("articles.article", Id, Account);
}
