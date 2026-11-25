using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed class NodeRegistration
{
    private readonly Dictionary<string, RegisteredNode> nodes;

    internal NodeRegistration(Dictionary<string, RegisteredNode> nodes) => this.nodes = nodes;

    public void Register(NodeDescriptor descriptor, Func<NodeContext, CancellationToken, Task> run) =>
        Register(descriptor, run, null);

    internal void Register(
        NodeDescriptor descriptor,
        Func<NodeContext, CancellationToken, Task> run,
        Func<byte[], JsonElement, CancellationToken, Task<byte[]>>? invokePacket,
        PacketExecutionMode? mode = null
    )
    {
        _ = descriptor.Inputs.ToDictionary(port => port.Name, StringComparer.Ordinal);
        _ = descriptor.Outputs.ToDictionary(port => port.Name, StringComparer.Ordinal);
        var snapshot = descriptor with
        {
            Inputs = Array.AsReadOnly(descriptor.Inputs.ToArray()),
            Outputs = Array.AsReadOnly(descriptor.Outputs.ToArray()),
        };
        if (!nodes.TryAdd(descriptor.Type, new(snapshot, run, invokePacket, mode)))
            throw new ArgumentException($"Node type already registered: {descriptor.Type}.", nameof(descriptor));
    }
}
