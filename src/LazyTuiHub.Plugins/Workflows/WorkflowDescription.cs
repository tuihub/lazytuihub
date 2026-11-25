using LazyTuiHub.Framework;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugins;

public sealed record WorkflowDescription(PipelineDefinition Definition, IReadOnlyList<WorkflowProblem> Problems)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public PipelinePlan? Plan { get; init; }
    public bool Executable => Plan is not null;

    public PipelinePlan ExecutablePlan() =>
        Plan
        ?? throw new WorkflowUnavailableException(
            string.Join(
                Environment.NewLine,
                Problems.Select(problem =>
                    $"{problem.NodeId}: {problem.NodeType} (plugin {problem.PluginId ?? "unknown"}): {problem.Reason}"
                )
            )
        );
}
