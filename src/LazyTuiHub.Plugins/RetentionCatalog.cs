using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

internal sealed class RetentionCatalog
{
    private readonly SqliteDatabase database;

    public RetentionCatalog(SqliteDatabase database)
    {
        this.database = database;
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS dao_retentions (
                owner TEXT NOT NULL, target TEXT NOT NULL, requirement TEXT NOT NULL, enabled INTEGER NOT NULL,
                last_cleaned TEXT, last_deleted INTEGER NOT NULL DEFAULT 0, last_error TEXT,
                PRIMARY KEY(owner, target)
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Observe(string owner, DaoRequirement requirement, DaoTable table)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dao_retentions(owner, target, requirement, enabled) VALUES ($owner, $target, $requirement, 1)
            ON CONFLICT(owner, target) DO UPDATE SET requirement = excluded.requirement
            """;
        command.AddValue("$owner", owner);
        command.AddValue("$target", Target(table));
        command.AddValue("$requirement", JsonSerializer.Serialize(requirement, PipelineStore.JsonOptions));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<RetentionRecord> Read()
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT owner, target, requirement, enabled, last_cleaned, last_deleted, last_error FROM dao_retentions ORDER BY owner, target";
        using var reader = command.ExecuteReader();
        var result = new List<RetentionRecord>();
        while (reader.Read())
            result.Add(
                new(
                    reader.GetString(reader.GetOrdinal("owner")),
                    JsonSerializer.Deserialize<DaoRequirement>(
                        reader.GetString(reader.GetOrdinal("requirement")),
                        PipelineStore.JsonOptions
                    )!,
                    JsonSerializer.Deserialize<DaoTable>(
                        reader.GetString(reader.GetOrdinal("target")),
                        PipelineStore.JsonOptions
                    )!,
                    reader.GetBoolean(reader.GetOrdinal("enabled")),
                    reader.IsDBNull(reader.GetOrdinal("last_cleaned"))
                        ? null
                        : DateTimeOffset.Parse(
                            reader.GetString(reader.GetOrdinal("last_cleaned")),
                            CultureInfo.InvariantCulture
                        ),
                    reader.GetInt32(reader.GetOrdinal("last_deleted")),
                    reader.IsDBNull(reader.GetOrdinal("last_error"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("last_error"))
                )
            );
        return result;
    }

    public void SetEnabled(string owner, DaoTable table, bool enabled)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dao_retentions SET enabled = $enabled WHERE owner = $owner AND target = $target";
        command.AddValue("$enabled", enabled);
        command.AddValue("$owner", owner);
        command.AddValue("$target", Target(table));
        if (command.ExecuteNonQuery() != 1)
            throw new KeyNotFoundException("Unknown retention delegation.");
    }

    public void Result(RetentionRecord record, DateTimeOffset time, int deleted, string? error)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE dao_retentions SET last_cleaned = $time, last_deleted = $deleted, last_error = $error WHERE owner = $owner AND target = $target";
        command.AddValue("$time", time.ToString("O", CultureInfo.InvariantCulture));
        command.AddValue("$deleted", deleted);
        command.AddValue("$error", error);
        command.AddValue("$owner", record.Owner);
        command.AddValue("$target", Target(record.Table));
        command.ExecuteNonQuery();
    }

    private static string Target(DaoTable table) => JsonSerializer.Serialize(table, PipelineStore.JsonOptions);
}
