using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace LazyTuiHub.Plugins;

[JsonConverter(typeof(PluginDependencyConverter))]
public sealed record PluginDependency(string Id, string? Version = null)
{
    internal string? Problem(PluginSpec? available, bool optional)
    {
        VersionRange? range = null;
        if (Version is not null && !VersionRange.TryParse(Version, out range))
            return $"Invalid version range '{Version}' for plugin '{Id}'.";
        if (available is null)
            return optional ? null : $"Required plugin '{Id}' is unavailable.";
        if (range is null)
            return null;
        var relation = optional ? "Compatibility with" : "Dependency on";
        return
            NuGetVersion.TryParse(available.Version, out var installed)
            && range.Satisfies(installed)
            && (range.Float?.Satisfies(installed) ?? true)
            ? null
            : $"{relation} plugin '{Id}' needs {Version}, found {available.Version ?? "no declared version"}.";
    }
}
