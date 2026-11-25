using LazyTuiHub.Interaction;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Plugin.TuiHub.Articles.Frontend;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal sealed class LocalArticleOperations(IArticleApi api, ArticleStore store) : ArticleOperations(api)
{
    [Capability("Search stored article text.")]
    public object Search(string query) => store.Search(query);
}
