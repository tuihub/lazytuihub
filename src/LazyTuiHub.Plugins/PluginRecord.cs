using System.Text.Json;

namespace LazyTuiHub.Plugins;

public sealed record PluginRecord(PluginSpec Spec, bool Enabled, JsonElement Settings, PluginDeclaration Declaration);
