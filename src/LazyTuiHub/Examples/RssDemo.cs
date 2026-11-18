using LazyTuiHub.Api;
using LazyTuiHub.Api.Articles;
using LazyTuiHub.Api.Articles.V1;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Client;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Examples;

public static class RssDemo
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lazytuihub-rss-demo-{Guid.NewGuid():N}");
        try
        {
            await using var app = ApplicationHost.Create([
                "--demo",
                "true",
                "--database",
                Path.Combine(directory, "workspace.db"),
            ]);
            app.MapGet("/fixture.xml", () => Results.Text(FeedXml, "application/rss+xml"));
            var workspace = app.Services.GetRequiredService<WorkflowRuntime>();
            var headlines = 0;
            var articleType = workspace
                .Registry.Descriptors.Single(node => string.Equals(node.Type, "rss.parse", StringComparison.Ordinal))
                .Outputs.Single()
                .DataType;
            workspace.Registry.Register(
                new("demo.headlines", "another-business", [new("input", articleType, PortDelivery.Streaming)], []),
                async (context, token) =>
                {
                    await foreach (var input in context.ReadEventsAsync())
                    {
                        token.ThrowIfCancellationRequested();
                        if (input is StreamInput)
                            Interlocked.Increment(ref headlines);
                    }
                }
            );
            var ingestion = workspace.Get("rss.ingest");
            workspace.Save(
                ingestion with
                {
                    Nodes = [.. ingestion.Nodes, PipelineNode.Create("headlines", "demo.headlines")],
                    Edges = [.. ingestion.Edges, new("parse", "output", "headlines", "input")],
                    BufferCapacity = 1,
                }
            );
            await app.StartAsync(app.Lifetime.ApplicationStopping);
            try
            {
                await CompareAsync(app, () => headlines);
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task CompareAsync(WebApplication app, Func<int> headlineCount)
    {
        const int fixtureEntries = 2;
        const int refreshAttempts = 2;
        var cancellationToken = app.Lifetime.ApplicationStopping;
        var local = app.Services.GetRequiredService<IRssApi>();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var remote = new RssHttpClient(http);
        var remoteArticles = new ArticleHttpClient(http);
        var localArticles = app.Services.GetRequiredService<IArticleApi>();
        var request = new CreateFeedRequest
        {
            Feed = new() { Uri = new Uri(http.BaseAddress, "fixture.xml").AbsoluteUri, DisplayName = "MVP fixture" },
            RequestId = Guid.NewGuid().ToString("D"),
        };
        var feed = await remote.CreateFeedAsync(request, cancellationToken);
        Require(
            feed.Equals(await local.CreateFeedAsync(request, cancellationToken)),
            "Creation request deduplication differs."
        );
        var refresh = await remote.RefreshFeedAsync(new() { Name = feed.Name }, cancellationToken);
        Require(
            refresh.ProcessedArticleCount == fixtureEntries,
            "The ingestion pipeline did not process both entries."
        );
        var query = new ListArticlesRequest { Filter = ArticleFilters.ForSource(feed.Name) };
        var articles = await remoteArticles.ListArticlesAsync(query, cancellationToken);
        Require(
            articles.Equals(await localArticles.ListArticlesAsync(query, cancellationToken)),
            "Local and HTTP articles differ."
        );
        Require(
            articles.Articles.Count == fixtureEntries
                && articles.Articles.Any(article => article.PublishTime is not null),
            "Missing entries or publication timestamps."
        );
        var article = articles.Articles[0];
        await remoteArticles.MarkArticleReadAsync(new() { Name = article.Name }, cancellationToken);
        await remote.RefreshFeedAsync(new() { Name = feed.Name }, cancellationToken);
        Require(
            (await localArticles.GetArticleAsync(new() { Name = article.Name }, cancellationToken)).Read,
            "Refresh lost read state."
        );
        Require(
            (await remoteArticles.ListArticlesAsync(query, cancellationToken)).Articles.Count == fixtureEntries,
            "Refresh duplicated entries."
        );
        Require(
            headlineCount() == fixtureEntries * refreshAttempts,
            "The cross-business branch did not receive every parsed entry."
        );
        Console.WriteLine(
            "RSS MVP: local = HTTP; SQLite deduplication and read state preserved; cross-business branch received every entry."
        );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public const string FeedXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0"><channel><title>MVP fixture</title>
          <item><guid>hello</guid><title>Hello streaming</title><link>/posts/hello</link>
            <description>One payload, multiple consumers.</description><pubDate>Sun, 15 Nov 2026 10:00:00 GMT</pubDate></item>
          <item><guid>world</guid><title>世界</title><link>/posts/world</link>
            <description>SQLite keeps read state across refreshes.</description></item>
        </channel></rss>
        """;
}
