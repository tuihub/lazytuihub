using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed class RssConflictException(string reason, string message) : Exception(message)
{
    public string Reason { get; } = reason;
}
