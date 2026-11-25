namespace LazyTuiHub.Games;

public sealed record GameAvailability(
    string Origin,
    string Provider,
    string Label,
    string SourceId,
    string Status,
    string? Location
);
