namespace LazyTuiHub.Dao;

public interface IRecordDao
{
    void CreateTable(DaoTable table);
    void Put<T>(DaoTable table, string key, T value, DateTimeOffset? expiresAt = null);
    IReadOnlyList<StoredRecord<T>> Read<T>(DaoTable table, DateTimeOffset now, bool includeExpired = false);
    bool Keep(DaoTable table, string key);
    int DeleteExpired(DaoTable table, DateTimeOffset now);
}
