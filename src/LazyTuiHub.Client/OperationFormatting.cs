using System.Text.Json;
using Google.Protobuf;

namespace LazyTuiHub.Client;

public static class OperationFormatting
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string Json(object value) =>
        value is IMessage message ? JsonFormatter.Default.Format(message) : JsonSerializer.Serialize(value, Options);
}
