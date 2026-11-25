using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed class NodeRegistry
{
    private readonly Dictionary<string, RegisteredNode> nodes = new(StringComparer.Ordinal);

    public NodeRegistry() => Registration = new(nodes);

    public NodeRegistration Registration { get; }

    public IEnumerable<NodeDescriptor> Descriptors => nodes.Values.Select(node => node.Descriptor);

    public AccountResourceAccess? DataAccess(string operation) =>
        nodes.GetValueOrDefault(operation)?.Descriptor.DataAccess;

    public IEnumerable<string> PacketOperations(PacketExecutionMode mode) =>
        nodes.Values.Where(node => node.Mode == mode).Select(node => node.Descriptor.Type);

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
