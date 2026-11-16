using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record FeedRecord(string Id, string Url, string DisplayName)
{
    public DataReference Reference => new("rss.feed", Id);
}
