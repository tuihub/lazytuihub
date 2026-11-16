namespace LazyTuiHub.Plugins;

public sealed record PluginTask(
    string Id,
    IReadOnlyList<string> Workflows,
    Func<IServiceProvider, CancellationToken, Task> Run
);
