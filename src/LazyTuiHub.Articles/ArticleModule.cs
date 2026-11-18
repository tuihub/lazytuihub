using LazyTuiHub.Api.Articles;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Articles;

public static class ArticleModule
{
    private const string Module = "articles";

    public static void Register(NodeRegistry registry, ArticleStore store)
    {
        BuiltInNodes.RegisterOutput<ArticleRecord>(registry, "articles.output");
        StreamNodes.Transform<ArticleDraft, ArticleRecord>(registry, "articles.store", Module, (packet, _, _) =>
            One(store.Save(packet.Value, packet.Reference))
        );
        StreamNodes.Source<ArticleRecord>(registry, "articles.read-articles", Module, (context, _) =>
            store
                .ReadArticles(context.Run.Input as ArticleQuery ?? new())
                .Select(article => new DataPacket<ArticleRecord>(article, article.Reference))
                .ToAsyncEnumerable()
        );
        StreamNodes.Source<ArticleRecord>(registry, "articles.read-article", Module, (context, _) =>
            One(store.GetArticle((string)context.Run.Input!))
        );
        StreamNodes.Source<ArticleRecord>(registry, "articles.mark-read", Module, (context, _) =>
            One(store.GetArticle((string)context.Run.Input!, markRead: true))
        );
    }

    public static IEnumerable<PipelineDefinition> Pipelines()
    {
        foreach (
            var (name, type) in new[]
            {
                ("articles.list", "articles.read-articles"),
                ("articles.get", "articles.read-article"),
                ("articles.mark-read", "articles.mark-read"),
            }
        )
            yield return new(
                name,
                [PipelineNode.Create("source", type), PipelineNode.Create("output", "articles.output")],
                [new("source", StreamNodes.Output, "output", StreamNodes.Input)]
            );
    }

    private static IAsyncEnumerable<DataPacket<ArticleRecord>> One(ArticleRecord article) =>
        new[] { new DataPacket<ArticleRecord>(article, article.Reference) }.ToAsyncEnumerable();
}
