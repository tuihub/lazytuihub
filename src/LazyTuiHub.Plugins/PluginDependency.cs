using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace LazyTuiHub.Plugins;

[JsonConverter(typeof(PluginDependencyConverter))]
public sealed record PluginDependency(string Id, string? Version = null)
{
    internal string? Problem(PluginSpec? available)
    {
        if (available is null)
            return $"Required plugin '{Id}' is unavailable.";
        if (Version is null)
            return null;
        if (!VersionRange.TryParse(Version, out var range))
            return $"Invalid version range '{Version}' for required plugin '{Id}'.";
        return
            NuGetVersion.TryParse(available.Version, out var installed)
            && range.Satisfies(installed)
            && (range.Float?.Satisfies(installed) ?? true)
            ? null
            : $"Required plugin '{Id}' needs {Version}, found {available.Version ?? "no declared version"}.";
    }
}
