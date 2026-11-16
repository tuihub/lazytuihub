namespace LazyTuiHub.Framework;

public sealed record DataRelation(DataReference Source, DataReference Target, string Kind);
