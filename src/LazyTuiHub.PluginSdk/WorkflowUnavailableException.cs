namespace LazyTuiHub.Plugins;

public sealed class WorkflowUnavailableException(string message) : InvalidOperationException(message);
