namespace LazyTuiHub.Plugins;

public sealed record DeclaredTask(string Id, IReadOnlyList<string> Workflows);
