using System.Text.Json;

namespace LazyTuiHub.Client;

public partial class SessionHttpClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
        settings.Converters.Add(new ProtobufJsonConverter());
}
