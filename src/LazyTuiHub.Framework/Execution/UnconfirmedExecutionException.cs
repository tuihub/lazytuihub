namespace LazyTuiHub.Framework;

public sealed class UnconfirmedExecutionException(string message, Exception cause) : Exception(message, cause);
