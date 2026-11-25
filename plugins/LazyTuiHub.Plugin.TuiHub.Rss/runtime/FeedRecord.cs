using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed record FeedRecord(string Id, string Url, string DisplayName, string Account = ResourceAccess.LocalAccount)
{
    public DataReference Reference => new("rss.feed", Id, Account);
}
