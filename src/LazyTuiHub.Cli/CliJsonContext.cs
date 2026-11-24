using System.Text.Json.Serialization;

namespace LazyTuiHub.Cli;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LoginOutput))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
