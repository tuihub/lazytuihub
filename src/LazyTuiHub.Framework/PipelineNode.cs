using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed record PipelineNode(string Id, string Type, JsonElement Settings, string? PluginId = null)
{
    public static PipelineNode Create(string id, string type, object? settings = null, string? pluginId = null) =>
        new(id, type, JsonSerializer.SerializeToElement(settings ?? new { }), pluginId);
}
