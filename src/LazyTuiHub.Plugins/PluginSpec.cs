namespace LazyTuiHub.Plugins;

public sealed record PluginSpec(
    string Id,
    string Name,
    string Assembly,
    string EntryType,
    bool Required = false,
    PluginDependency[]? Requires = null,
    bool RemotePage = false,
    string? Version = null
);
