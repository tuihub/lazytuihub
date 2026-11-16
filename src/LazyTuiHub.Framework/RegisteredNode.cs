using System.Runtime.CompilerServices;

namespace LazyTuiHub.Framework;

internal sealed record RegisteredNode(NodeDescriptor Descriptor, NodeOperator Run);
