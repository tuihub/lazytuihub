namespace LazyTuiHub.Framework;

public sealed record NodeProgress(
    string NodeId,
    string State,
    string? MessageKey = null,
    IReadOnlyList<string>? MissingPorts = null
);
