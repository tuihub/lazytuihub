using System.Text.Json;

namespace LazyTuiHub.Framework;

internal sealed record RegisteredNode(
    NodeDescriptor Descriptor,
    Func<NodeContext, CancellationToken, Task> Run,
    Func<byte[], JsonElement, CancellationToken, Task<byte[]>>? InvokePacket,
    PacketExecutionMode? Mode
);
