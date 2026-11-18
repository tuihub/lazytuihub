namespace LazyTuiHub.Framework;

public sealed class PipelinePlanner(NodeRegistry registry, ExecutionSnapshot snapshot)
{
    public PipelinePlan Create(PipelineDefinition definition)
    {
        if (definition.BufferCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition), "The queue capacity must be positive.");
        var nodes = definition.Nodes.ToDictionary(
            node => node.Id,
            node =>
            {
                var registration = registry.Get(node.Type);
                var requirements = (node.Requires ?? [])
                    .Concat(
                        registration.Descriptor.RequiredResource is { } resource ? [resource] : Array.Empty<string>()
                    )
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new PlannedNode(
                    node,
                    registration,
                    snapshot.Place(node.Id, registration, requirements),
                    requirements
                );
            },
            StringComparer.Ordinal
        );
        if (nodes.Count == 0)
            throw new ArgumentException("A pipeline needs at least one node.", nameof(definition));
        var edges = definition.Edges.ToArray();
        if (edges.Distinct().Count() != edges.Length)
            throw new ArgumentException("A pipeline cannot contain duplicate connections.", nameof(definition));
        foreach (var edge in edges)
            ValidateConnection(edge, nodes);
        foreach (var node in nodes.Values)
        {
            var missing = node.Descriptor.Inputs.FirstOrDefault(port =>
                !edges.Any(edge =>
                    string.Equals(edge.Target, node.Definition.Id, StringComparison.Ordinal)
                    && string.Equals(edge.TargetPort, port.Name, StringComparison.Ordinal)
                )
            );
            if (missing is not null)
                throw new ArgumentException(
                    $"Input '{node.Definition.Id}.{missing.Name}' has no producer.",
                    nameof(definition)
                );
        }
        ValidateAcyclic(nodes.Keys, edges);
        return new(definition.Name, nodes.Values.ToArray(), edges, definition.BufferCapacity, snapshot);
    }

    private static void ValidateConnection(PipelineEdge edge, Dictionary<string, PlannedNode> nodes)
    {
        if (!nodes.TryGetValue(edge.Source, out var source) || !nodes.TryGetValue(edge.Target, out var target))
            throw new ArgumentException($"Unknown endpoint: {edge.Source} -> {edge.Target}.", nameof(edge));
        var output = source.Descriptor.Outputs.SingleOrDefault(port =>
            string.Equals(port.Name, edge.SourcePort, StringComparison.Ordinal)
        );
        var input = target.Descriptor.Inputs.SingleOrDefault(port =>
            string.Equals(port.Name, edge.TargetPort, StringComparison.Ordinal)
        );
        if (output is null || input is null || output.DataType != input.DataType || output.Delivery != input.Delivery)
            throw new ArgumentException(
                $"Incompatible connection: {edge.Source}.{edge.SourcePort} -> {edge.Target}.{edge.TargetPort}.",
                nameof(edge)
            );
    }

    private static void ValidateAcyclic(IEnumerable<string> ids, PipelineEdge[] edges)
    {
        var incoming = ids.ToDictionary(
            id => id,
            id => edges.Count(edge => string.Equals(edge.Target, id, StringComparison.Ordinal)),
            StringComparer.Ordinal
        );
        var ready = new Queue<string>(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (ready.TryDequeue(out var id))
        {
            visited++;
            foreach (
                var target in edges
                    .Where(edge => string.Equals(edge.Source, id, StringComparison.Ordinal))
                    .Select(edge => edge.Target)
            )
                if (--incoming[target] == 0)
                    ready.Enqueue(target);
        }
        if (visited != incoming.Count)
            throw new ArgumentException("The local executor currently requires a DAG.", nameof(edges));
    }
}
