using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal static class ArticleModule
{
    private const string Module = "articles";

    public static void Register(NodeRegistration registry, ArticleStore store)
    {
        StreamNodes.RegisterOutput<ArticleRecord>(registry, "articles.output", Module);
        registry.RegisterOwned<ArticleDraft, ArticleRecord>(
            new(
                "articles.store",
                Module,
                [PortDescriptor.Stream<ArticleDraft>(StreamNodes.Input)],
                [PortDescriptor.Stream<ArticleRecord>(StreamNodes.Output)],
                ExecutionTopology.PrimaryWorkspace,
                new(PluginProject.Id, "articles", AccessMode.Write)
            ),
            (packet, _, _) =>
            {
                var article = store.Save(packet.Value, packet.Reference);
                return Task.FromResult(new DataPacket<ArticleRecord>(article, article.Reference));
            }
        );
        StreamNodes.Source<ArticleRecord>(
            registry,
            "articles.read-articles",
            Module,
            (context, _) =>
                store
                    .ReadArticles(context.Run.Input as ArticleQuery ?? new())
                    .Select(article => new DataPacket<ArticleRecord>(article, article.Reference))
                    .ToAsyncEnumerable(),
            ExecutionTopology.PrimaryWorkspace
        );
        StreamNodes.Source<ArticleRecord>(
            registry,
            "articles.read-article",
            Module,
            (context, _) => One(store.GetArticle((string)context.Run.Input!)),
            ExecutionTopology.PrimaryWorkspace
        );
        StreamNodes.Source<ArticleRecord>(
            registry,
            "articles.mark-read",
            Module,
            (context, _) => One(store.GetArticle((string)context.Run.Input!, markRead: true)),
            ExecutionTopology.PrimaryWorkspace
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
            yield return StreamWorkflow.Chain(
                name,
                [PipelineNode.Create("source", type), PipelineNode.Create("output", "articles.output")]
            );
    }

    private static IAsyncEnumerable<DataPacket<ArticleRecord>> One(ArticleRecord article) =>
        new[] { new DataPacket<ArticleRecord>(article, article.Reference) }.ToAsyncEnumerable();
}
