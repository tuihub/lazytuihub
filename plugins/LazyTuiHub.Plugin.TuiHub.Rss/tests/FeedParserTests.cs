using System.Text;
using System.Xml;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Rss;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class FeedParserTests
{
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
}
