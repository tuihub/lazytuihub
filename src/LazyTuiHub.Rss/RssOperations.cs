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
            if (packet.Payload is ArticleRecord)
                Interlocked.Increment(ref count);
            return ValueTask.CompletedTask;
        }
    }

    public async Task<IReadOnlyList<T>> RunAsync<T>(
        string pipeline,
        object? input = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var result = new List<T>();
        await workflows.RunAsync(pipeline, new(input, Collect), cancellationToken);
        return result;

        ValueTask Collect(DataPacket packet, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (packet.Payload is T item)
            {
                lock (result)
                    result.Add(item);
            }
            return ValueTask.CompletedTask;
        }
    }
}
