using Microsoft.Data.Sqlite;

namespace LazyTuiHub.Framework;

public sealed class SqliteDatabase
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
                type TEXT NOT NULL,
                key TEXT NOT NULL,
                PRIMARY KEY (type, key)
            );
            CREATE TABLE IF NOT EXISTS data_relations (
                source_type TEXT NOT NULL,
                source_key TEXT NOT NULL,
                target_type TEXT NOT NULL,
                target_key TEXT NOT NULL,
                kind TEXT NOT NULL,
                PRIMARY KEY (source_type, source_key, target_type, target_key, kind),
                FOREIGN KEY (source_type, source_key) REFERENCES data_objects(type, key),
                FOREIGN KEY (target_type, target_key) REFERENCES data_objects(type, key)
            );
            CREATE TABLE IF NOT EXISTS pipelines (
                name TEXT PRIMARY KEY NOT NULL,
                definition TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

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
