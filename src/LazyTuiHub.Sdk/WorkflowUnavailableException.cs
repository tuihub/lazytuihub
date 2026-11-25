namespace LazyTuiHub.Sdk;

public sealed class WorkflowUnavailableException(string message) : InvalidOperationException(message);
