using System.Globalization;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public static class RssModule
{
    public const string OutputNode = "output";

    public static void Register(NodeRegistry registry, RssStore store)
    {
        registry.RegisterTransform<TimerTick, FetchRequest>("rss.feed-requests", "rss", (_, _, context, _) =>
            store
                .ReadFeeds(context.Input as FeedSelection ?? new())
                .Select(feed => new DataPacket<FetchRequest>(new(new(feed.Url), feed.Reference), feed.Reference))
                .ToAsyncEnumerable()
        );
        registry.RegisterTransform<HttpDocument, ArticleDraft>("rss.parse", "rss", (packet, _, _, token) =>
            FeedParser
                .Parse(packet.Value, token)
                .Select(article => new DataPacket<ArticleDraft>(article, packet.Reference))
                .ToAsyncEnumerable()
        );
        registry.RegisterTransform<ArticleDraft, ArticleRecord>("rss.store", "rss", (packet, _, _, _) =>
            One(store.Save(packet.Value))
        );
        registry.RegisterSource<FeedRecord>("rss.subscribe", "rss", (_, context, _) =>
            One(store.Subscribe((SubscribeFeed)context.Input!))
        );
        registry.RegisterSource<FeedRecord>("rss.read-feeds", "rss", (_, context, _) =>
            store
                .ReadFeeds(context.Input as FeedSelection ?? new())
                .Select(feed => new DataPacket<FeedRecord>(feed, feed.Reference))
                .ToAsyncEnumerable()
        );
        registry.RegisterSource<ArticleRecord>("rss.read-articles", "rss", (_, context, _) =>
            store
                .ReadArticles(context.Input as ArticleQuery ?? new())
                .Select(article => new DataPacket<ArticleRecord>(article, article.Reference))
                .ToAsyncEnumerable()
        );
        registry.RegisterSource<ArticleRecord>("rss.read-article", "rss", (_, context, _) =>
            One(store.GetArticle((ArticleSelection)context.Input!))
        );
        registry.RegisterSource<ArticleRecord>("rss.mark-read", "rss", (_, context, _) =>
            One(store.GetArticle((ArticleSelection)context.Input!, markRead: true))
        );
    }

    public static IEnumerable<PipelineDefinition> Pipelines(TimeSpan refreshInterval)
    {
        yield return new(
            "rss.ingest",
            [
                PipelineNode.Create(
                    "timer",
                    "framework.timer",
                    new { interval = refreshInterval.ToString("c", CultureInfo.InvariantCulture) }
                ),
                PipelineNode.Create("feeds", "rss.feed-requests"),
                PipelineNode.Create("fetch", "framework.http-fetch"),
                PipelineNode.Create("parse", "rss.parse"),
                PipelineNode.Create("store", "rss.store"),
                PipelineNode.Create(OutputNode, "framework.output"),
            ],
            [
                new("timer", "feeds"),
                new("feeds", "fetch"),
                new("fetch", "parse"),
                new("parse", "store"),
                new("store", OutputNode),
            ]
        );
        foreach (
            var (name, type) in new[]
            {
                ("rss.subscribe", "rss.subscribe"),
                ("rss.feeds", "rss.read-feeds"),
                ("rss.articles", "rss.read-articles"),
                ("rss.article", "rss.read-article"),
                ("rss.mark-read", "rss.mark-read"),
            }
        )
            yield return new(
                name,
                [PipelineNode.Create("source", type), PipelineNode.Create(OutputNode, "framework.output")],
                [new("source", OutputNode)]
            );
    }

    private static IAsyncEnumerable<DataPacket<FeedRecord>> One(FeedRecord feed) =>
        new[] { new DataPacket<FeedRecord>(feed, feed.Reference) }.ToAsyncEnumerable();

    private static IAsyncEnumerable<DataPacket<ArticleRecord>> One(ArticleRecord article) =>
        new[] { new DataPacket<ArticleRecord>(article, article.Reference) }.ToAsyncEnumerable();
}
