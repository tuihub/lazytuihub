namespace LazyTuiHub.Dao;

public sealed record DaoCapability(DaoMode Mode, string Contract, string Instance, string Backend);
