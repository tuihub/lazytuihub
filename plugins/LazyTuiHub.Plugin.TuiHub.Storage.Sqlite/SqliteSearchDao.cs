using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LazyTuiHub.Dao;

namespace LazyTuiHub.Plugin.TuiHub.Storage.Sqlite;

[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "DaoNames emits hexadecimal identifiers; all user data and FTS queries are parameterized."
)]
[SuppressMessage(
    "Major Code Smell",
    "S2077:Formatting SQL queries is security-sensitive",
    Justification = "DaoNames emits identifiers containing only fixed prefixes and hexadecimal characters."
)]
public sealed class SqliteSearchDao(IConnectionSource source) : ISqliteSearch
{
    public void CreateTable(DaoTable table)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE VIRTUAL TABLE IF NOT EXISTS {DaoNames.Table(table)} USING fts5(id UNINDEXED, account UNINDEXED, text)";
        command.ExecuteNonQuery();
    }

    public void Index(DaoTable table, string key, string text, DbTransaction transaction, string account = "local")
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"DELETE FROM {DaoNames.Table(table)} WHERE account = $account AND id = $id; INSERT INTO {DaoNames.Table(table)}(id, account, text) VALUES ($id, $account, $text)";
        command.AddValue("$account", account);
        command.AddValue("$id", key);
        command.AddValue("$text", text);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<string> Search(
        DaoTable table,
        string query,
        string account = "local",
        IReadOnlyList<string>? allowed = null
    )
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        var fts = DaoNames.Table(table);
        command.CommandText = $"""
            SELECT id FROM {fts} WHERE {fts} MATCH $query AND account = $account
                AND ($allowed IS NULL OR id IN (SELECT value FROM json_each($allowed)))
            ORDER BY rank, id
            """;
        command.AddValue("$account", account);
        command.AddValue("$allowed", allowed is null ? DBNull.Value : JsonSerializer.Serialize(allowed));
        command.AddValue("$query", query);
        using var reader = command.ExecuteReader();
        var keys = new List<string>();
        while (reader.Read())
            keys.Add(reader.GetString(0));
        return keys;
    }

    public static bool Available(IConnectionSource source)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE VIRTUAL TABLE temp.dao_fts5_probe USING fts5(value)";
        try
        {
            command.ExecuteNonQuery();
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
