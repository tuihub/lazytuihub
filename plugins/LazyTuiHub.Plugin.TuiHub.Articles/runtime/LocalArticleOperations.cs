using LazyTuiHub.Api;
using LazyTuiHub.Client;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Articles;

internal sealed class LocalArticleOperations(IArticleApi api, ArticleStore store) : ArticleOperations(api)
{
    [Capability("Search stored article text.")]
    public object Search(string query) => store.Search(query);
}
