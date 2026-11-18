namespace LazyTuiHub.Framework;

public sealed class PipelinePlan
{
    internal PipelinePlan(
        string name,
        PlannedNode[] nodes,
        PipelineEdge[] connections,
        int bufferCapacity,
        ExecutionSnapshot snapshot
    )
    {
        Name = name;
        Inventory = snapshot;
        Nodes = Array.AsReadOnly(nodes);
        Connections = Array.AsReadOnly(connections);
        BufferCapacity = bufferCapacity;
    }

    public ExecutionSnapshot Inventory { get; }
    public string Name { get; }
    public IReadOnlyList<PlannedNode> Nodes { get; }
    public IReadOnlyList<PipelineEdge> Connections { get; }
    public int BufferCapacity { get; }
}
