using System.Globalization;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Rss;

public static class RssModule
{
    public const string OutputNode = "output";
    private const string SubscribeNodeType = "rss.subscribe";

    public static void Register(NodeRegistry registry, RssStore store)
    {
        BuiltInNodes.RegisterOutput<FeedRecord>(registry, "rss.output-feeds");
        BuiltInNodes.RegisterOutput<ArticleRecord>(registry, "rss.output-articles");
        StreamNodes.Transform<TimerTick, FetchRequest>(registry, "rss.feed-requests", "rss", (_, context, _) =>
            store
                .ReadFeeds(context.Run.Input as FeedSelection ?? new())
                .Select(feed => new DataPacket<FetchRequest>(new(new(feed.Url), feed.Reference), feed.Reference))
                .ToAsyncEnumerable()
        );
        StreamNodes.Transform<HttpDocument, ArticleDraft>(registry, "rss.parse", "rss", (packet, _, token) =>
            FeedParser
                .Parse(packet.Value, token)
                .Select(article => new DataPacket<ArticleDraft>(article, packet.Reference))
                .ToAsyncEnumerable()
        );
        StreamNodes.Transform<ArticleDraft, ArticleRecord>(registry, "rss.store", "rss", (packet, _, _) =>
            One(store.Save(packet.Value))
        );
        StreamNodes.Source<FeedRecord>(registry, SubscribeNodeType, "rss", (context, _) =>
            One(store.Subscribe((SubscribeFeed)context.Run.Input!))
        );
        StreamNodes.Source<FeedRecord>(registry, "rss.read-feeds", "rss", (context, _) =>
            store
                .ReadFeeds(context.Run.Input as FeedSelection ?? new())
                .Select(feed => new DataPacket<FeedRecord>(feed, feed.Reference))
                .ToAsyncEnumerable()
        );
        StreamNodes.Source<ArticleRecord>(registry, "rss.read-articles", "rss", (context, _) =>
            store
                .ReadArticles(context.Run.Input as ArticleQuery ?? new())
                .Select(article => new DataPacket<ArticleRecord>(article, article.Reference))
                .ToAsyncEnumerable()
        );
        StreamNodes.Source<ArticleRecord>(registry, "rss.read-article", "rss", (context, _) =>
            One(store.GetArticle((ArticleSelection)context.Run.Input!))
        );
        StreamNodes.Source<ArticleRecord>(registry, "rss.mark-read", "rss", (context, _) =>
            One(store.GetArticle((ArticleSelection)context.Run.Input!, markRead: true))
        );
    }

    public static IEnumerable<PipelineDefinition> Pipelines(TimeSpan refreshInterval)
    {
        yield return new(
            "rss.ingest",
            [
                PipelineNode.Create(
                    "timer",
                    "builtin.timer",
                    new { interval = refreshInterval.ToString("c", CultureInfo.InvariantCulture) }
                ),
                PipelineNode.Create("feeds", "rss.feed-requests"),
                PipelineNode.Create("fetch", "builtin.http-fetch"),
                PipelineNode.Create("parse", "rss.parse"),
                PipelineNode.Create("store", "rss.store"),
                PipelineNode.Create(OutputNode, "rss.output-articles"),
            ],
            [
                new("timer", StreamNodes.Output, "feeds", StreamNodes.Input),
                new("feeds", StreamNodes.Output, "fetch", StreamNodes.Input),
                new("fetch", StreamNodes.Output, "parse", StreamNodes.Input),
                new("parse", StreamNodes.Output, "store", StreamNodes.Input),
                new("store", StreamNodes.Output, OutputNode, StreamNodes.Input),
            ]
        );
        foreach (
            var (name, type) in new[]
            {
                (SubscribeNodeType, SubscribeNodeType),
                ("rss.feeds", "rss.read-feeds"),
                ("rss.articles", "rss.read-articles"),
                ("rss.article", "rss.read-article"),
                ("rss.mark-read", "rss.mark-read"),
            }
        )
            yield return new(
                name,
                [
                    PipelineNode.Create("source", type),
                    PipelineNode.Create(
                        OutputNode,
                        type is SubscribeNodeType or "rss.read-feeds" ? "rss.output-feeds" : "rss.output-articles"
                    ),
                ],
                [new("source", StreamNodes.Output, OutputNode, StreamNodes.Input)]
            );
    }

    private static IAsyncEnumerable<DataPacket<FeedRecord>> One(FeedRecord feed) =>
        new[] { new DataPacket<FeedRecord>(feed, feed.Reference) }.ToAsyncEnumerable();

    private static IAsyncEnumerable<DataPacket<ArticleRecord>> One(ArticleRecord article) =>
        new[] { new DataPacket<ArticleRecord>(article, article.Reference) }.ToAsyncEnumerable();
}
