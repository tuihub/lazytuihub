using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public sealed class RssWorkspace
{
    public RssWorkspace(SqliteDatabase database, HttpClient httpClient, TimeSpan refreshInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(refreshInterval, TimeSpan.Zero);
        RefreshInterval = refreshInterval;
        Store = new(database);
        Pipelines = new(database);
        Catalog = new(database);
        Registry = new();
        BuiltInNodes.Register(Registry, httpClient);
        RssModule.Register(Registry, Store);
        Runner = new(Registry);
        foreach (var pipeline in RssModule.Pipelines(refreshInterval))
        {
            Runner.Validate(pipeline);
            Pipelines.Save(pipeline, replace: false);
        }
    }

    public RssStore Store { get; }
    public PipelineStore Pipelines { get; }
    public DataCatalog Catalog { get; }
    public NodeRegistry Registry { get; }
    public PipelineRunner Runner { get; }
    public TimeSpan RefreshInterval { get; }

    public async Task<int> RefreshAsync(FeedSelection selection, CancellationToken cancellationToken = default)
    {
        var count = 0;
        await Runner.RunAsync(Pipelines.Get("rss.ingest"), new(selection, Count), cancellationToken);
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
        await Runner.RunAsync(Pipelines.Get(pipeline), new(input, Collect), cancellationToken);
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
