namespace LazyTuiHub.Plugins;

public sealed record PluginHooks(
    Func<IServiceProvider, CancellationToken, Task>? Cleanup = null,
    Func<IServiceProvider, CancellationToken, Task>? Uninstall = null
);
