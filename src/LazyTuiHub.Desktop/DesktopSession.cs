namespace LazyTuiHub.Desktop;

public sealed class DesktopSession : IDisposable
{
    private readonly CancellationTokenSource stopping = new();
    private readonly HashSet<Task> pending = [];
    private readonly Lock sync = new();
    private bool stopped;
    public bool IsStopping
    {
        get
        {
            lock (sync)
                return stopped;
        }
    }

    public Task RunAsync(Func<CancellationToken, Task> action)
    {
        lock (sync)
        {
            if (stopped)
                return Task.CompletedTask;
            var task = action(stopping.Token);
            pending.Add(task);
            _ = task.ContinueWith(
                completed =>
                {
                    lock (sync)
                        pending.Remove(completed);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            return task;
        }
    }

    public async Task StopAsync()
    {
        Task[] tasks;
        lock (sync)
        {
            stopped = true;
            tasks = pending.ToArray();
        }
        await stopping.CancelAsync();
        await Task.WhenAll(tasks);
    }

    public void Dispose() => stopping.Dispose();
}
