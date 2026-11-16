namespace LazyTuiHub.Framework;

public sealed record NodeDescriptor(
    string Type,
    string Module,
    IReadOnlyList<PortDescriptor> Inputs,
    IReadOnlyList<PortDescriptor> Outputs
);
