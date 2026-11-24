using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Google.Protobuf;

namespace LazyTuiHub.Client;

internal sealed partial class ProtobufJsonResolver : IJsonTypeInfoResolver
{
    internal static ProtobufJsonResolver Instance { get; } = new();

    private static JsonTypeInfo<T> Contract<T>(JsonSerializerOptions options)
        where T : IMessage<T>, new() => JsonMetadataServices.CreateValueInfo<T>(options, new MessageConverter<T>());

    private sealed class MessageConverter<T> : JsonConverter<T>
        where T : IMessage<T>, new()
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return JsonParser.Default.Parse<T>(document.RootElement.GetRawText());
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteRawValue(JsonFormatter.Default.Format(value));
    }
}
