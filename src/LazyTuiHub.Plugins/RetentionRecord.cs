using LazyTuiHub.Dao;

namespace LazyTuiHub.Plugins;

public sealed record RetentionRecord(
    string Owner,
    DaoRequirement Requirement,
    DaoTable Table,
    bool Enabled,
    DateTimeOffset? LastCleaned,
    int LastDeleted,
    string? LastError
);
