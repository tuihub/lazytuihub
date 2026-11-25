namespace LazyTuiHub.Examples;

public static class RssFixture
{
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
