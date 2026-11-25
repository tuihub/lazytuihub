namespace LazyTuiHub.Framework;

public sealed class PlannedNode
{
    internal PlannedNode(
        PipelineNode definition,
        RegisteredNode registration,
        ExecutionTarget[] targets,
        string[] requirements
    )
    {
        Definition = definition;
        Registration = registration;
        Targets = targets;
        Requirements = Array.AsReadOnly(requirements);
        Candidates = Array.AsReadOnly(targets.Select(target => target.Instance).ToArray());
    }

    public PipelineNode Definition { get; }
    public NodeDescriptor Descriptor => Registration.Descriptor;
    public IReadOnlyList<string> Requirements { get; }
    public string ExecutionMode => Registration.Mode?.ToString() ?? "Local";
    public string Instance => Candidates[0];
    public IReadOnlyList<string> Candidates { get; }
    internal IReadOnlyList<ExecutionTarget> Targets { get; }
    internal RegisteredNode Registration { get; }
}
