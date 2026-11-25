using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Sdk;

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
            CREATE TABLE IF NOT EXISTS plugin_frontends (id TEXT NOT NULL, role TEXT NOT NULL, declaration TEXT NOT NULL, PRIMARY KEY(id, role));
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
        reader.Close();
        command.CommandText = "SELECT id, declaration FROM plugin_frontends ORDER BY id, role";
        using var entries = command.ExecuteReader();
        var contributions = new Dictionary<string, List<FrontendDeclaration>>(StringComparer.Ordinal);
        while (entries.Read())
        {
            var id = entries.GetString(0);
            if (!contributions.TryGetValue(id, out var list))
            {
                list = [];
                contributions.Add(id, list);
            }
            list.Add(JsonSerializer.Deserialize<FrontendDeclaration>(entries.GetString(1), Json)!);
        }
        return result
            .Select(record =>
            {
                if (!contributions.TryGetValue(record.Spec.Id, out var list))
                    return record;
                var backend = record.Declaration;
                return record with
                {
                    Declaration = backend with
                    {
                        Pages = [.. backend.Pages, .. list.SelectMany(role => role.Pages)],
                        Commands =
                        [
                            .. backend
                                .Commands.Concat(list.SelectMany(role => role.Commands))
                                .Distinct(StringComparer.Ordinal),
                        ],
                    },
                };
            })
            .ToArray();
    }

    private sealed record FrontendDeclaration(IReadOnlyList<DeclaredPage> Pages, IReadOnlyList<string> Commands);

    public void RecordFrontends(
        PluginSpec spec,
        IEnumerable<(PluginEntryPointKind Kind, PluginServices Registration)> entries
    )
    {
        using var connection = database.Open();
        using var tx = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.Parameters.AddWithValue(IdParameter, spec.Id);
        command.CommandText = """
            DELETE FROM plugin_frontends WHERE id = $id AND
                ((role = 'Client' AND $client IS NULL) OR (role = 'ClientGui' AND $gui IS NULL) OR (role = 'ClientCli' AND $cli IS NULL))
            """;
        command.Parameters.AddWithValue("$client", (object?)spec.EntryPoints.Client ?? DBNull.Value);
        command.Parameters.AddWithValue("$gui", (object?)spec.EntryPoints.ClientGui ?? DBNull.Value);
        command.Parameters.AddWithValue("$cli", (object?)spec.EntryPoints.ClientCli ?? DBNull.Value);
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.Parameters.AddWithValue(IdParameter, spec.Id);
        command.CommandText =
            "INSERT OR REPLACE INTO plugin_frontends(id,role,declaration) VALUES($id,$role,$declaration)";
        var roleParameter = command.Parameters.Add("$role", Microsoft.Data.Sqlite.SqliteType.Text);
        var declaration = command.Parameters.Add("$declaration", Microsoft.Data.Sqlite.SqliteType.Text);
        foreach (var (role, registration) in entries)
        {
            roleParameter.Value = role.ToString();
            declaration.Value = JsonSerializer.Serialize(
                new FrontendDeclaration([.. registration.Pages], [.. registration.Capabilities.Keys]),
                Json
            );
            command.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void Observe(PluginSpec spec, bool present)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO plugin_catalog(id, specification, enabled, settings, declaration)
            VALUES ($id, $spec, 1, '{}', $empty)
            ON CONFLICT(id) DO UPDATE SET specification = excluded.specification WHERE $present
            """;
        command.Parameters.AddWithValue(IdParameter, spec.Id);
        command.Parameters.AddWithValue("$present", present);
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
                Commands = registration.Capabilities.Keys.ToArray(),
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
