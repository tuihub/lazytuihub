namespace LazyTuiHub.Framework;

public sealed class InstanceUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
