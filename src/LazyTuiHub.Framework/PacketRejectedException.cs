namespace LazyTuiHub.Framework;

public sealed class PacketRejectedException(string message, Exception cause) : Exception(message, cause);
