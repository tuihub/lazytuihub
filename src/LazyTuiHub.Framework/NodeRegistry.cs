using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed class NodeRegistry
{
    private readonly Dictionary<string, RegisteredNode> nodes = new(StringComparer.Ordinal);

    public IEnumerable<NodeDescriptor> Descriptors => nodes.Values.Select(node => node.Descriptor);

    public IEnumerable<string> PacketOperations(PacketExecutionMode mode) =>
        nodes.Values.Where(node => node.Mode == mode).Select(node => node.Descriptor.Type);

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

    public Task<byte[]> InvokePacketAsync(
        string operation,
        PacketExecutionMode mode,
        IReadOnlySet<string> resources,
        byte[] input,
        JsonElement settings,
        CancellationToken token
    )
    {
        if (!nodes.TryGetValue(operation, out var node) || node.InvokePacket is null || node.Mode != mode)
            throw new InstanceUnavailableException($"Packet operation '{operation}' is unavailable.");
        if (node.Descriptor.RequiredResource is { } required && !resources.Contains(required))
            throw new InstanceUnavailableException($"This instance does not offer '{required}'.");
        return node.InvokePacket(input, settings, token);
    }

    public void Import(NodeRegistry source)
    {
        var duplicate = source.nodes.Keys.FirstOrDefault(nodes.ContainsKey);
        if (duplicate is not null)
            throw new ArgumentException($"Node type already registered: {duplicate}.", nameof(source));
        foreach (var (key, value) in source.nodes)
            nodes.Add(key, value);
    }

    internal RegisteredNode Get(string type) =>
        nodes.TryGetValue(type, out var node)
            ? node
            : throw new ArgumentException($"Unknown node type: {type}.", nameof(type));
}
