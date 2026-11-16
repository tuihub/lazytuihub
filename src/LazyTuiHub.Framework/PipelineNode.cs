using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed record PipelineNode(string Id, string Type, JsonElement Settings)
{
    public static PipelineNode Create(string id, string type, object? settings = null) =>
        new(id, type, JsonSerializer.SerializeToElement(settings ?? new { }));
}
