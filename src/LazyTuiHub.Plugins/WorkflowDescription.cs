using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

public sealed record WorkflowDescription(PipelineDefinition Definition, IReadOnlyList<WorkflowProblem> Problems)
{
    public bool Executable => Problems.Count == 0;
}
