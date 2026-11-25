namespace LazyTuiHub.Framework;

internal sealed record ExecutionTarget(string Instance, IExecutionPeer? Peer);
