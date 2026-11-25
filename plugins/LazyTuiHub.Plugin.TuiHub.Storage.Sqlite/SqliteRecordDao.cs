using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LazyTuiHub.Dao;

namespace LazyTuiHub.Plugin.TuiHub.Storage.Sqlite;

[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and index identifiers are generated as hexadecimal by DaoNames; values use parameters."
)]
[SuppressMessage(
    "Major Code Smell",
    "S2077:Formatting SQL queries is security-sensitive",
    Justification = "DaoNames emits identifiers containing only fixed prefixes and hexadecimal characters."
)]
public sealed class SqliteRecordDao(IConnectionSource source) : IRecordDao
{
    private const string KeyParameter = "$key";
    private const string ExpiryParameter = "$expiry";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void CreateTable(DaoTable table)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS {DaoNames.Table(table)} (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL, expires_at INTEGER); CREATE INDEX IF NOT EXISTS {DaoNames.Index(table, "expiry")} ON {DaoNames.Table(table)}(expires_at)";
        command.ExecuteNonQuery();
    }

    public void Put<T>(DaoTable table, string key, T value, DateTimeOffset? expiresAt = null)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO {DaoNames.Table(table)}(key, value, expires_at) VALUES ($key, $value, $expiry) ON CONFLICT(key) DO UPDATE SET value = excluded.value, expires_at = excluded.expires_at";
        command.AddValue(KeyParameter, key);
        command.AddValue("$value", JsonSerializer.Serialize(value, Json));
        command.AddValue(ExpiryParameter, expiresAt?.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<StoredRecord<T>> Read<T>(DaoTable table, DateTimeOffset now, bool includeExpired = false)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT key, value, expires_at FROM {DaoNames.Table(table)} WHERE $all OR expires_at IS NULL OR expires_at > $now ORDER BY key";
        command.AddValue("$all", includeExpired);
        command.AddValue("$now", now.ToUnixTimeMilliseconds());
        using var reader = command.ExecuteReader();
        var result = new List<StoredRecord<T>>();
        while (reader.Read())
            result.Add(
                new(
                    reader.GetString(reader.GetOrdinal("key")),
                    JsonSerializer.Deserialize<T>(reader.GetString(reader.GetOrdinal("value")), Json)!,
                    reader.IsDBNull(reader.GetOrdinal("expires_at"))
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(reader.GetOrdinal("expires_at")))
                )
            );
        return result;
    }

    public bool Keep(DaoTable table, string key)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {DaoNames.Table(table)} SET expires_at = NULL WHERE key = $key";
        command.AddValue(KeyParameter, key);
        return command.ExecuteNonQuery() == 1;
    }

    public int DeleteExpired(DaoTable table, DateTimeOffset now)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {DaoNames.Table(table)} WHERE expires_at <= $expiry";
        command.AddValue(ExpiryParameter, now.ToUnixTimeMilliseconds());
        return command.ExecuteNonQuery();
    }
}
