namespace LazyTuiHub.Dao;

public sealed record StoredRecord<T>(string Key, T Value, DateTimeOffset? ExpiresAt);
