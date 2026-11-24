namespace LazyTuiHub.Cli;

internal sealed record LoginOutput(string Token, string Account, DateTimeOffset Expires);
