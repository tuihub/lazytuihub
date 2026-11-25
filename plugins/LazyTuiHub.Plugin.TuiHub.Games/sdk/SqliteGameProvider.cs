using System.Diagnostics.CodeAnalysis;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games;

public abstract class SqliteGameProvider(string provider) : GameProviderPlugin(provider)
{
    private const int CannotOpenDatabase = 14;
    private string database = string.Empty;

    public string? AvailableCatalog => File.Exists(database) ? Provider : null;

    public override void Configure(PluginRegistration registration)
    {
        database = registration.Configuration[$"Games:{Provider}:Database"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyTuiHub",
                "catalogs",
                Provider + ".sqlite"
            );
        registration.Services.AddSingleton<SqliteGameProvider>(this);
        base.Configure(registration);
    }

    protected const string MatchingTitles = """
        SELECT id FROM game_titles WHERE game_titles MATCH '"' || replace($key,'"','""') || '"' AND length($key)>=3
        UNION ALL SELECT id FROM game_titles WHERE length($key)<3 AND instr(lower(title),lower($key))>0
        """;

    protected abstract string Query(bool search);
    protected abstract GameMetadata Read(SqliteDataReader row);

    protected override Task<GameMetadata[]> SearchAsync(HttpClient client, string query, CancellationToken token) =>
        Task.Run(() => Fetch(query, true, token), token);

    protected override async Task<GameMetadata> LookupAsync(HttpClient client, string id, CancellationToken token) =>
        (await Task.Run(() => Fetch(id, false, token), token)).SingleOrDefault()
        ?? throw new KeyNotFoundException($"No {Provider} game with ID '{id}'.");

    [SuppressMessage(
        "Security",
        "CA2100",
        Justification = "Each provider declares fixed SQL and the query value is bound."
    )]
    private GameMetadata[] Fetch(string value, bool search, CancellationToken token)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = database,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString()
        );
        try
        {
            connection.Open();
        }
        catch (SqliteException error) when (error.SqliteErrorCode == CannotOpenDatabase)
        {
            throw new InstanceUnavailableException(
                $"Cannot open {Provider} catalog at {database}. Configure Games:{Provider}:Database.",
                error
            );
        }
        using var command = connection.CreateCommand();
        command.CommandText = Query(search);
        command.Parameters.AddWithValue("$key", value);
        using var reader = command.ExecuteReader();
        var rows = new List<GameMetadata>();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            rows.Add(Read(reader));
        }
        return rows.ToArray();
    }

    protected static string? Optional(SqliteDataReader row, string column) =>
        row[column] is string value ? value : null;
}
