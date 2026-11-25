using LazyTuiHub.Api;
using LazyTuiHub.Api.Articles;
using LazyTuiHub.Api.Articles.V1;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Client;

public class ArticleOperations(IArticleApi api)
{
    [Capability("List articles, optionally filtered by source and read status.")]
    public Task<ListArticlesResponse> List(
        CancellationToken token,
        string? source = null,
        string? filter = null,
        bool unread = false,
        string pageToken = ""
    ) =>
        api.ListArticlesAsync(
            new()
            {
                Filter = ArticleFilters.ForSource(source, filter ?? (unread ? "read = false" : string.Empty)),
                PageToken = pageToken,
            },
            token
        );

    [Capability("Open an article by its resource name.")]
    public Task<Article> Get(string name, CancellationToken token) => api.GetArticleAsync(new() { Name = name }, token);

    [Capability("Mark an article as read.")]
    public Task<Article> Read(string name, CancellationToken token) =>
        api.MarkArticleReadAsync(new() { Name = name }, token);
}
