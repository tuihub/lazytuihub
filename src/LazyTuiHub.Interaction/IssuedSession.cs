namespace LazyTuiHub.Interaction;

public sealed record IssuedSession(string Token, string Account, DateTimeOffset Expires);
