namespace LazyTuiHub.Framework;

public sealed class AccessRequiredException() : InvalidOperationException("A valid user session is required.");
