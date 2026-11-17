namespace UiComparison;

public sealed record PluginResult<TView>(IPrototypePage<TView>? Page, string Status);
