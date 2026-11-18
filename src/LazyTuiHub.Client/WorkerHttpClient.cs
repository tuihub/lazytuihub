using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using LazyTuiHub.Api;

namespace LazyTuiHub.Client;

public partial class WorkerHttpClient : IWorkerApi
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new ProtobufJsonConverter());
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "NSwag declares this partial hook as an instance method."
    )]
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, StringBuilder urlBuilder)
    {
        // Keep the custom-method colon from being parsed as a URI scheme.
        urlBuilder.Insert(0, "./");
    }
}
