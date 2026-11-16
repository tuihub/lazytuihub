namespace LazyTuiHub.Framework;

public sealed class PlannedNode
{
    internal PlannedNode(PipelineNode definition, RegisteredNode registration, string instance)
    {
        Definition = definition;
        Registration = registration;
        Instance = instance;
    }

    public PipelineNode Definition { get; }
    public NodeDescriptor Descriptor => Registration.Descriptor;
    public string Instance { get; }
    internal RegisteredNode Registration { get; }
}
