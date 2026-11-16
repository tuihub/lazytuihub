using System.Text.Json;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Commands;

internal static class CommandJson
{
    public static void Write<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, PipelineStore.JsonOptions));

    public static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Provide --{key}.", nameof(configuration));

    public static object Port(PortDescriptor port) =>
        new
        {
            port.Name,
            Type = port.DataType.FullName,
            Delivery = port.Delivery.ToString(),
        };
}
