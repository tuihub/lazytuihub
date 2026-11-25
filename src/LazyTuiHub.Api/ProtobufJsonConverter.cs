using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;

namespace LazyTuiHub.Client;

public sealed class ProtobufJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeof(IMessage).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(MessageConverter<>).MakeGenericType(typeToConvert))!;

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
