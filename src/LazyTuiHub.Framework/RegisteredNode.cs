namespace LazyTuiHub.Framework;

internal sealed record RegisteredNode(NodeDescriptor Descriptor, Func<NodeContext, CancellationToken, Task> Run);
