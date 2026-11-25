using Grpc.Core;
using LazyTuiHub.Api.Articles.V1;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using ArticleContract = LazyTuiHub.Api.Articles.V1.ArticleService;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal sealed class ArticleService(IArticleApi application) : ArticleContract.ArticleServiceBase
{
    public override Task<ListArticlesResponse> ListArticles(ListArticlesRequest request, ServerCallContext context) =>
        application.ListArticlesAsync(request, context.CancellationToken);

    public override Task<Article> GetArticle(GetArticleRequest request, ServerCallContext context) =>
        application.GetArticleAsync(request, context.CancellationToken);

    public override Task<Article> MarkArticleRead(MarkArticleReadRequest request, ServerCallContext context) =>
        application.MarkArticleReadAsync(request, context.CancellationToken);
}
