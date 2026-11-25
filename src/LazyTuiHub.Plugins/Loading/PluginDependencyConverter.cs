using System.Text.Json;
using System.Text.Json.Serialization;

namespace LazyTuiHub.Plugins;

internal sealed class PluginDependencyConverter : JsonConverter<PluginDependency>
{
    public override bool HandleNull => true;

    public override PluginDependency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new(reader.GetString()!);
        using var json = JsonDocument.ParseValue(ref reader);
        var entry = json.RootElement;
        if (
            entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
        )
            throw new JsonException("A plugin dependency needs an ID.");
        if (!entry.TryGetProperty("version", out var version) || version.ValueKind == JsonValueKind.Null)
            return new(id.GetString()!);
        if (version.ValueKind != JsonValueKind.String)
            throw new JsonException($"The version constraint for '{id.GetString()}' must be a string.");
        return new(id.GetString()!, version.GetString());
    }

    public override void Write(Utf8JsonWriter writer, PluginDependency value, JsonSerializerOptions options)
    {
        if (value.Version is null)
        {
            writer.WriteStringValue(value.Id);
            return;
        }
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("version", value.Version);
        writer.WriteEndObject();
    }
}
