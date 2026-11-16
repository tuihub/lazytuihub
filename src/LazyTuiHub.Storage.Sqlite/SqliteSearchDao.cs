using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using LazyTuiHub.Dao;

namespace LazyTuiHub.Storage.Sqlite;

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
            $"CREATE VIRTUAL TABLE IF NOT EXISTS {DaoNames.Table(table)} USING fts5(id UNINDEXED, text)";
        command.ExecuteNonQuery();
    }

    public void Index(DaoTable table, string key, string text, DbTransaction transaction)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"DELETE FROM {DaoNames.Table(table)} WHERE id = $id; INSERT INTO {DaoNames.Table(table)}(id, text) VALUES ($id, $text)";
        command.AddValue("$id", key);
        command.AddValue("$text", text);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<string> Search(DaoTable table, string query)
    {
        using var connection = source.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT id FROM {DaoNames.Table(table)} WHERE {DaoNames.Table(table)} MATCH $query ORDER BY rank, id";
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
