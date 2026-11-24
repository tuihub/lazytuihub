namespace LazyTuiHub.Games;

public sealed record GameDetails(
    GameEntry Entry,
    IReadOnlyList<GameMetadata> Sources,
    IReadOnlyList<GameSession> Sessions
);
