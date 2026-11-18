using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

public static class WorkflowResults
{
    public static async Task<IReadOnlyList<T>> CollectAsync<T>(
        this IWorkflowExecutor workflows,
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
