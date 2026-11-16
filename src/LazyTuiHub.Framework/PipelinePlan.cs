namespace LazyTuiHub.Framework;

public sealed class PipelinePlan
{
    internal PipelinePlan(string name, PlannedNode[] nodes, PipelineEdge[] connections, int bufferCapacity)
    {
        Name = name;
        Nodes = Array.AsReadOnly(nodes);
        Connections = Array.AsReadOnly(connections);
        BufferCapacity = bufferCapacity;
    }

    public string Name { get; }
    public IReadOnlyList<PlannedNode> Nodes { get; }
    public IReadOnlyList<PipelineEdge> Connections { get; }
    public int BufferCapacity { get; }
}
