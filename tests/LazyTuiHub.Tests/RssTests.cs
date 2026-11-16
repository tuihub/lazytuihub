using System.Net;
using System.Text;
using System.Xml;
using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using LazyTuiHub.Rss;
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
            var workspace = new RssWorkspace(new(path), http, TimeSpan.FromMinutes(15));
            var request = new SubscribeFeed(
                string.Empty,
                new("https://example.com/feed.xml"),
                "Example",
                Guid.NewGuid().ToString("D")
            );
            var feed = Assert.Single(
                await workspace.RunAsync<FeedRecord>(
                    "rss.subscribe",
                    request,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
            Assert.Equal(
                2,
                await workspace.RefreshAsync(new(feed.Id), cancellationToken: TestContext.Current.CancellationToken)
            );
            var articles = await workspace.RunAsync<ArticleRecord>(
                "rss.articles",
                cancellationToken: TestContext.Current.CancellationToken
            );
            var article = articles[0];
            await workspace.RunAsync<ArticleRecord>(
                "rss.mark-read",
                new ArticleSelection(feed.Id, article.Id),
                cancellationToken: TestContext.Current.CancellationToken
            );
            workspace.Pipelines.Save(workspace.Pipelines.Get("rss.ingest") with { BufferCapacity = 1 });

            var restarted = new RssWorkspace(new(path), http, TimeSpan.FromMinutes(15));
            Assert.Equal(
                feed,
                Assert.Single(
                    await restarted.RunAsync<FeedRecord>(
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
            var refreshed = await restarted.RunAsync<ArticleRecord>(
                "rss.articles",
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Equal(2, refreshed.Count);
            Assert.True(refreshed.Single(value => value.Id == article.Id).IsRead);
            Assert.Contains(refreshed, value => value.Article.Title == "Updated title");
            Assert.Equal(2, restarted.Catalog.ReadRelations(feed.Reference).Count);
            Assert.Equal(1, restarted.Pipelines.Get("rss.ingest").BufferCapacity);
            var conflict = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
                restarted.RunAsync<FeedRecord>(
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
