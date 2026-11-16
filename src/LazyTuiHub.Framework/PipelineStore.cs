using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed class PipelineStore(SqliteDatabase database)
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void Save(PipelineDefinition definition, bool replace = true)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        if (replace)
            command.CommandText =
                "INSERT INTO pipelines(name, definition) VALUES ($name, $json) ON CONFLICT(name) DO UPDATE SET definition = excluded.definition";
        else
            command.CommandText =
                "INSERT INTO pipelines(name, definition) VALUES ($name, $json) ON CONFLICT DO NOTHING";
        command.Parameters.AddWithValue("$name", definition.Name);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(definition, JsonOptions));
        command.ExecuteNonQuery();
    }

    public PipelineDefinition Get(string name)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT definition FROM pipelines WHERE name = $name";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() is string json
            ? JsonSerializer.Deserialize<PipelineDefinition>(json, JsonOptions)!
            : throw new KeyNotFoundException($"Pipeline not found: {name}.");
    }
}
