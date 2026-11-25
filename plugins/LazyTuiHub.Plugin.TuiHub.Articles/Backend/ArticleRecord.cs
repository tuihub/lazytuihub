using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal sealed record ArticleRecord(
    long Sequence,
    string Id,
    ArticleDraft Article,
    bool IsRead,
    string Account = ResourceAccess.LocalAccount
)
{
    public DataReference Reference => new("articles.article", Id, Account);
}
