namespace LazyTuiHub.Dao;

public sealed record DaoRequirement(
    string Name,
    DaoMode Mode,
    string Contract,
    string Instance = "workspace",
    string? Backend = null
);
