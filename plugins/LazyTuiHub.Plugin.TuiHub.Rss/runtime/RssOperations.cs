using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Rss;

public sealed class RssOperations(IWorkflowExecutor workflows, TimeSpan refreshInterval)
{
    public TimeSpan RefreshInterval { get; } = refreshInterval;

    public Task ExecuteAsync(string pipeline, PipelineContext context, CancellationToken token) =>
        workflows.RunAsync(pipeline, context, token);

    public async Task<int> RefreshAsync(FeedSelection selection, CancellationToken cancellationToken = default)
    {
        var count = 0;
        await workflows.RunAsync("rss.ingest", new(selection, Count), cancellationToken);
        return count;

        ValueTask Count(DataPacket packet, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (packet.Reference is { Type: "articles.article" })
                Interlocked.Increment(ref count);
            return ValueTask.CompletedTask;
        }
    }
}
