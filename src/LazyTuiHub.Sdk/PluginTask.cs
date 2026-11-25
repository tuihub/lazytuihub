namespace LazyTuiHub.Sdk;

public sealed record PluginTask(
    string Id,
    IReadOnlyList<string> Workflows,
    Func<IServiceProvider, CancellationToken, Task> Run
);
