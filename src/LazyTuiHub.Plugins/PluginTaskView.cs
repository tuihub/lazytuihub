namespace LazyTuiHub.Plugins;

public sealed record PluginTaskView(string PluginId, DeclaredTask Declaration, string State, string? Reason);
