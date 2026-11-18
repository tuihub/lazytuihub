namespace LazyTuiHub.Framework;

public sealed record NodeProgress(
    string NodeId,
    string State,
    string? MessageKey = null,
    IReadOnlyList<string>? MissingPorts = null,
    string? Instance = null,
    string? Detail = null,
    DataReference? Reference = null
);
