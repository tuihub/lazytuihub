namespace LazyTuiHub.Plugins;

public sealed record DeclaredNode(
    string Type,
    string Module,
    IReadOnlyList<DeclaredPort> Inputs,
    IReadOnlyList<DeclaredPort> Outputs,
    string? RequiredResource = null
);
