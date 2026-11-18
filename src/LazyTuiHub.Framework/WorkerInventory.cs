namespace LazyTuiHub.Framework;

public sealed record WorkerInventory(
    string Instance,
    IReadOnlySet<string> Resources,
    IReadOnlySet<string> RepeatableOperations,
    IReadOnlySet<string> OwnedOperations
);
