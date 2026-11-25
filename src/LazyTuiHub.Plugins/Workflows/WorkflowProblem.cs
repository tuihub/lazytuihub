namespace LazyTuiHub.Plugins;

public sealed record WorkflowProblem(string? NodeId, string? NodeType, string? PluginId, string Reason);
