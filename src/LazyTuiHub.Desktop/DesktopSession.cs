namespace LazyTuiHub.Desktop;

public sealed class DesktopSession : IDisposable
{
    private readonly CancellationTokenSource stopping = new();
    private readonly HashSet<Task> pending = [];
    public bool IsStopping => stopping.IsCancellationRequested;

    public async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsStopping)
            return;
        var task = action(stopping.Token);
        pending.Add(task);
        try
        {
            await task;
        }
        finally
        {
            pending.Remove(task);
        }
    }

    public async Task StopAsync()
    {
        await stopping.CancelAsync();
        await Task.WhenAll(pending.ToArray());
    }

    public void Dispose() => stopping.Dispose();
}
