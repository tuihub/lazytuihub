using LazyTuiHub.Api.Articles.V1;

namespace LazyTuiHub.Api;

public interface IArticleApi
{
    Task<ListArticlesResponse> ListArticlesAsync(
        ListArticlesRequest request,
        CancellationToken cancellationToken = default
    );
    Task<Article> GetArticleAsync(GetArticleRequest request, CancellationToken cancellationToken = default);
    Task<Article> MarkArticleReadAsync(MarkArticleReadRequest request, CancellationToken cancellationToken = default);
}
