using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Sdk;

public static class StreamWorkflow
{
    public static PipelineDefinition Chain(string name, IReadOnlyList<PipelineNode> nodes)
    {
        var steps = nodes.ToArray();
        var edges = steps
            .Zip(steps.Skip(1), (source, target) =>
                new PipelineEdge(source.Id, StreamNodes.Output, target.Id, StreamNodes.Input)
            )
            .ToArray();
        return new(name, steps, edges);
    }
}
