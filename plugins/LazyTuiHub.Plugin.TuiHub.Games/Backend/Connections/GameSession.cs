namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed record GameSession(string Device, DateTimeOffset Start, TimeSpan Duration);
