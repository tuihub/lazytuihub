using System.Data.Common;
using LazyTuiHub.Dao;
using Microsoft.Data.Sqlite;

namespace LazyTuiHub.Framework;

public sealed class SqliteDatabase : IConnectionSource
{
    private readonly string connectionString;

    public SqliteDatabase(string filename)
    {
        var fullPath = Path.GetFullPath(filename);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS data_objects (
                account TEXT NOT NULL,
                type TEXT NOT NULL,
                key TEXT NOT NULL,
                PRIMARY KEY (account, type, key)
            );
            CREATE TABLE IF NOT EXISTS data_relations (
                account TEXT NOT NULL,
                source_type TEXT NOT NULL,
                source_key TEXT NOT NULL,
                target_type TEXT NOT NULL,
                target_key TEXT NOT NULL,
                kind TEXT NOT NULL,
                PRIMARY KEY (account, source_type, source_key, target_type, target_key, kind),
                FOREIGN KEY (account, source_type, source_key) REFERENCES data_objects(account, type, key),
                FOREIGN KEY (account, target_type, target_key) REFERENCES data_objects(account, type, key)
            );
            CREATE TABLE IF NOT EXISTS pipelines (
                name TEXT PRIMARY KEY NOT NULL,
                definition TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    DbConnection IConnectionSource.Open() => Open();

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
