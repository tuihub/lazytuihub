namespace LazyTuiHub.Games;

public sealed record GameSession(string Device, DateTimeOffset Start, TimeSpan Duration);
