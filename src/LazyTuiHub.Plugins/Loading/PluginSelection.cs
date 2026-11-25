namespace LazyTuiHub.Plugins;

public sealed record PluginSelection(string Id, string Assembly, bool Required = false);
