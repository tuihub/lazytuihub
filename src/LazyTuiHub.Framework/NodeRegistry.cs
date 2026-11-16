namespace LazyTuiHub.Framework;

public sealed class NodeRegistry
{
    private readonly Dictionary<string, RegisteredNode> nodes = new(StringComparer.Ordinal);

    public IEnumerable<NodeDescriptor> Descriptors => nodes.Values.Select(node => node.Descriptor);

    public void Register(NodeDescriptor descriptor, Func<NodeContext, CancellationToken, Task> run)
    {
        _ = descriptor.Inputs.ToDictionary(port => port.Name, StringComparer.Ordinal);
        _ = descriptor.Outputs.ToDictionary(port => port.Name, StringComparer.Ordinal);
        var snapshot = descriptor with
        {
            Inputs = Array.AsReadOnly(descriptor.Inputs.ToArray()),
            Outputs = Array.AsReadOnly(descriptor.Outputs.ToArray()),
        };
        if (!nodes.TryAdd(descriptor.Type, new(snapshot, run)))
            throw new ArgumentException($"Node type already registered: {descriptor.Type}.", nameof(descriptor));
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
