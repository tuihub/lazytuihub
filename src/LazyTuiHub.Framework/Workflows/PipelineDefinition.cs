using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed record PipelineDefinition(
    string Name,
    IReadOnlyList<PipelineNode> Nodes,
    IReadOnlyList<PipelineEdge> Edges,
    int BufferCapacity = 16
);
