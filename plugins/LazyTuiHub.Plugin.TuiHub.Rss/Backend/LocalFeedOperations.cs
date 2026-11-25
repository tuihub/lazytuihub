using LazyTuiHub.Interaction;
using LazyTuiHub.Plugin.TuiHub.Rss.Api;
using LazyTuiHub.Plugin.TuiHub.Rss.Frontend;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed class LocalFeedOperations(IRssApi api, RssDetails details) : FeedOperations(api)
{
    [Capability("List captured feed details.")]
    public object Details(bool includeExpired = false) => details.Read(includeExpired);

    [Capability("Keep a captured detail beyond its retention period.")]
    public object KeepDetail(string key) => new { Kept = details.Keep(key) };
}
