using System.Globalization;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal static class RssModule
{
    public const string OutputNode = "output";
    private const string SubscribeNodeType = "rss.subscribe";

    public static void Register(NodeRegistration registry, RssStore store, RssDetails details)
    {
        StreamNodes.RegisterOutput<FeedRecord>(registry, "rss.output-feeds", "rss");
        StreamNodes.Transform<TimerTick, FetchRequest>(registry, "rss.feed-requests", "rss", (_, context, _) =>
            store
                .ReadFeeds(context.Run.Input as FeedSelection ?? new())
                .Select(feed => new DataPacket<FetchRequest>(new(new(feed.Url), feed.Reference), feed.Reference))
                .ToAsyncEnumerable()
        );
        StreamNodes.Transform<HttpDocument, ArticleDraft>(registry, "rss.parse", "rss", (packet, _, token) =>
        {
            details.Capture(packet.Value);
            return FeedParser
                .Parse(packet.Value, token)
                .Select(article => new DataPacket<ArticleDraft>(article, packet.Value.Source))
                .ToAsyncEnumerable();
        });
        StreamNodes.Source<FeedRecord>(registry, SubscribeNodeType, "rss", (context, _) =>
            One(store.Subscribe((SubscribeFeed)context.Run.Input!))
        );
        StreamNodes.Source<FeedRecord>(registry, "rss.read-feeds", "rss", (context, _) =>
            store
                .ReadFeeds(context.Run.Input as FeedSelection ?? new())
                .Select(feed => new DataPacket<FeedRecord>(feed, feed.Reference))
                .ToAsyncEnumerable()
        );
    }

    public static IEnumerable<PipelineDefinition> Pipelines(TimeSpan refreshInterval)
    {
        yield return StreamWorkflow.Chain(
            "rss.ingest",
            [
                PipelineNode.Create(
                    "timer",
                    "builtin.timer",
                    new { interval = refreshInterval.ToString("c", CultureInfo.InvariantCulture) },
                    pluginId: PluginProject.Core
                ),
                PipelineNode.Create("feeds", "rss.feed-requests"),
                PipelineNode.Create("fetch", "builtin.http-fetch", pluginId: PluginProject.Core),
                PipelineNode.Create("parse", "rss.parse"),
                PipelineNode.Create("store", "articles.store", pluginId: PluginProject.Articles),
                PipelineNode.Create(OutputNode, "articles.output", pluginId: PluginProject.Articles),
            ]
        );
        foreach (var (name, type) in new[] { (SubscribeNodeType, SubscribeNodeType), ("rss.feeds", "rss.read-feeds") })
            yield return StreamWorkflow.Chain(
                name,
                [PipelineNode.Create("source", type), PipelineNode.Create(OutputNode, "rss.output-feeds")]
            );
    }

    private static IAsyncEnumerable<DataPacket<FeedRecord>> One(FeedRecord feed) =>
        new[] { new DataPacket<FeedRecord>(feed, feed.Reference) }.ToAsyncEnumerable();
}
