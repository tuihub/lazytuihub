using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

internal sealed class PluginCatalog
{
    private readonly SqliteDatabase database;
    private const string IdParameter = "$id";
    private static JsonSerializerOptions Json => PipelineStore.JsonOptions;

    public PluginCatalog(SqliteDatabase database)
    {
        this.database = database;
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS plugin_catalog (
                id TEXT PRIMARY KEY NOT NULL,
                specification TEXT NOT NULL,
                enabled INTEGER NOT NULL,
                settings TEXT NOT NULL,
                declaration TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<PluginRecord> Read()
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT specification, enabled, settings, declaration FROM plugin_catalog ORDER BY id";
        using var reader = command.ExecuteReader();
        var result = new List<PluginRecord>();
        while (reader.Read())
            result.Add(
                new(
                    JsonSerializer.Deserialize<PluginSpec>(reader.GetString(0), Json)!,
                    reader.GetBoolean(1),
                    JsonSerializer.Deserialize<JsonElement>(reader.GetString(reader.GetOrdinal("settings"))),
                    JsonSerializer.Deserialize<PluginDeclaration>(
                        reader.GetString(reader.GetOrdinal("declaration")),
                        Json
                    )!
                )
            );
        return result;
    }

    public void Observe(PluginSpec spec)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO plugin_catalog(id, specification, enabled, settings, declaration)
            VALUES ($id, $spec, 1, '{}', $empty)
            ON CONFLICT(id) DO UPDATE SET specification = excluded.specification
            """;
        command.Parameters.AddWithValue(IdParameter, spec.Id);
        command.Parameters.AddWithValue("$spec", JsonSerializer.Serialize(spec, Json));
        command.Parameters.AddWithValue("$empty", JsonSerializer.Serialize(PluginDeclaration.Empty, Json));
        command.ExecuteNonQuery();
    }

    public void SetEnabled(string id, bool enabled)
    {
        var record = Read().Single(item => string.Equals(item.Spec.Id, id, StringComparison.Ordinal));
        if (record.Spec.Required && !enabled)
            throw new InvalidOperationException($"Core plugin '{id}' cannot be disabled.");
        Update(id, "enabled", enabled ? "1" : "0");
    }

    public void SetSettings(string id, JsonElement settings) => Update(id, "settings", settings.GetRawText());

    public void Declare(string id, PluginDeclaration declaration) =>
        Update(id, "declaration", JsonSerializer.Serialize(declaration, Json));

    public void Contributions(PluginRegistration registration)
    {
        var current = Read().Single(record => string.Equals(record.Spec.Id, registration.Id, StringComparison.Ordinal));
        Declare(
            registration.Id,
            current.Declaration with
            {
                DataTypes = registration.DataTypes.ToArray(),
                Commands = registration.Commands.Select(command => command.Name).ToArray(),
                Tasks = registration.Tasks.Select(task => new DeclaredTask(task.Id, task.Workflows)).ToArray(),
                HasCleanup = registration.Hooks.Cleanup is not null,
                HasUninstall = registration.Hooks.Uninstall is not null,
                DaoRequirements = registration.Daos.Requirements.ToArray(),
                DaoOffers = registration.DaoServices.Select(service => service.Capability).ToArray(),
                Pages = registration.Pages.ToArray(),
            }
        );
    }

    private void Update(string id, string column, string value)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        switch (column)
        {
            case "enabled":
                command.CommandText = "UPDATE plugin_catalog SET enabled = $value WHERE id = $id";
                break;
            case "settings":
                command.CommandText = "UPDATE plugin_catalog SET settings = $value WHERE id = $id";
                break;
            case "declaration":
                command.CommandText = "UPDATE plugin_catalog SET declaration = $value WHERE id = $id";
                break;
            default:
                throw new ArgumentException("Unknown catalog column.", nameof(column));
        }
        command.Parameters.AddWithValue(IdParameter, id);
        command.Parameters.AddWithValue("$value", value);
        if (command.ExecuteNonQuery() != 1)
            throw new KeyNotFoundException($"Unknown plugin '{id}'.");
    }
}
