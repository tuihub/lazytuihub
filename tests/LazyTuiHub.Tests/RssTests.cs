using System.Net;
using System.Text;
using System.Xml;
using LazyTuiHub.Articles;
using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using LazyTuiHub.Rss;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class RssTests
{
    [Fact]
    public async Task RefreshPersistsIdentityRelationsAndReadStateAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lazytuihub-rss-test-{Guid.NewGuid():N}");
        using var handler = new FeedHandler();
        using var http = new HttpClient(handler);
        try
        {
            var path = Path.Combine(directory, "workspace.db");
            await using var app = PluginTestHost.Create(path, http);
            var workspace = app.Services.GetRequiredService<RssOperations>();
            var workflows = app.Services.GetRequiredService<WorkflowRuntime>();
            var request = new SubscribeFeed(
                string.Empty,
                new("https://example.com/feed.xml"),
                "Example",
                Guid.NewGuid().ToString("D")
            );
            var feed = Assert.Single(
                await workflows.CollectAsync<FeedRecord>(
                    "rss.subscribe",
                    request,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
            Assert.Equal(
                2,
                await workspace.RefreshAsync(new(feed.Id), cancellationToken: TestContext.Current.CancellationToken)
            );
            var articles = await workflows.CollectAsync<ArticleRecord>(
                "articles.list",
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Null(Assert.Single(app.Services.GetRequiredService<RssDetails>().Read()).ExpiresAt);
            Assert.Single(app.Services.GetRequiredService<ArticleStore>().Search("streaming"));
            var article = articles[0];
            await workflows.CollectAsync<ArticleRecord>(
                "articles.mark-read",
                article.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
            workflows.Save(workflows.Get("rss.ingest") with { BufferCapacity = 1 });

            await using var restartedApp = PluginTestHost.Create(path, http);
            var restarted = restartedApp.Services.GetRequiredService<RssOperations>();
            var restartedWorkflows = restartedApp.Services.GetRequiredService<WorkflowRuntime>();
            Assert.Equal(
                feed,
                Assert.Single(
                    await restartedWorkflows.CollectAsync<FeedRecord>(
                        "rss.subscribe",
                        request,
                        cancellationToken: TestContext.Current.CancellationToken
                    )
                )
            );
            handler.Xml = RssDemo.FeedXml.Replace("Hello streaming", "Updated title", StringComparison.Ordinal);
            Assert.Equal(
                2,
                await restarted.RefreshAsync(new(feed.Id), cancellationToken: TestContext.Current.CancellationToken)
            );
            var refreshed = await restartedWorkflows.CollectAsync<ArticleRecord>(
                "articles.list",
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Equal(2, refreshed.Count);
            Assert.Empty(restartedApp.Services.GetRequiredService<ArticleStore>().Search("streaming"));
            Assert.Single(restartedApp.Services.GetRequiredService<ArticleStore>().Search("Updated"));
            Assert.Equal(2, restartedApp.Services.GetRequiredService<RssDetails>().Read().Count);
            Assert.True(refreshed.Single(value => value.Id == article.Id).IsRead);
            Assert.Contains(refreshed, value => value.Article.Title == "Updated title");
            Assert.Equal(2, restartedWorkflows.Catalog.ReadRelations(feed.Reference).Count);
            Assert.Equal(1, restartedWorkflows.Get("rss.ingest").BufferCapacity);
            var conflict = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
                restartedWorkflows.CollectAsync<FeedRecord>(
                    "rss.subscribe",
                    request with
                    {
                        Url = new("https://example.org/other"),
                    },
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
            Assert.IsType<RssConflictException>(conflict.InnerException);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ParserReadsAtomAndResolvesLinksWithoutLoadingExternalEntities()
    {
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>Example</title>
              <entry><id>urn:entry:1</id><title>世界</title><link href="../article"/>
                <content type="html">&lt;p&gt;Body&lt;/p&gt;</content><updated>2026-11-15T10:00:00Z</updated></entry>
              <entry><id>urn:entry:2</id><title>Second</title><summary>Summary</summary></entry>
            </feed>
            """;
        var source = new DataReference("rss.feed", "feed-id");
        var articles = FeedParser
            .Parse(
                new(new("https://example.com/feeds/atom"), Encoding.UTF8.GetBytes(atom), source),
                TestContext.Current.CancellationToken
            )
            .ToArray();
        Assert.Equal(2, articles.Length);
        Assert.Equal("https://example.com/article", articles[0].Url);
        Assert.Equal("<p>Body</p>", articles[0].Content);
        Assert.Equal("世界", articles[0].Title);
        Assert.NotNull(articles[0].PublishedAt);
        Assert.Throws<XmlException>(() =>
            FeedParser
                .Parse(
                    new(
                        new("https://example.com/feed"),
                        Encoding.UTF8.GetBytes(
                            "<!DOCTYPE rss [<!ENTITY content SYSTEM 'file:///not-read'>]><rss><channel>&content;</channel></rss>"
                        ),
                        source
                    ),
                    TestContext.Current.CancellationToken
                )
                .ToArray()
        );
    }

    [Fact]
    public async Task LocalAndGeneratedHttpClientsExerciseTheSamePipelines() => await RssDemo.RunAsync();

    [Fact]
    public void ParserHonorsXmlEncodingAndPreservesXhtmlMarkup()
    {
        const string rss =
            "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><rss><channel><item><guid>cafe</guid><title>café</title></item></channel></rss>";
        var source = new DataReference("rss.feed", "encoding");
        var article = Assert.Single(
            FeedParser.Parse(
                new(new("https://example.com/rss"), Encoding.Latin1.GetBytes(rss), source),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal("café", article.Title);
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom"><entry><id>xhtml</id>
            <content type="xhtml"><div xmlns="http://www.w3.org/1999/xhtml"><p>A <a href="https://example.com">link</a></p></div></content>
            </entry></feed>
            """;
        article = Assert.Single(
            FeedParser.Parse(
                new(new("https://example.com/atom"), Encoding.UTF8.GetBytes(atom), source),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Contains("href=\"https://example.com\"", article.Content);
        Assert.Contains("<p>", article.Content);
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        public string Xml { get; set; } = RssDemo.FeedXml;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Xml) });
    }
}
