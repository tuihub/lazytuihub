namespace LazyTuiHub.Framework;

public sealed record BusinessChange<T>(
    T Value,
    IReadOnlyList<DataReference> Objects,
    IReadOnlyList<DataRelation> Relations,
    IReadOnlyList<DataRelation>? RemovedRelations = null
);
