using System.Text.Json;
using System.Text.RegularExpressions;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

public static partial class PluginManifest
{
    [GeneratedRegex(
        @"\Alazytuihub\.plugin\.(?:[a-z0-9]+(?:-[a-z0-9]+)*\.)+[a-z0-9]+(?:-[a-z0-9]+)*\z",
        RegexOptions.NonBacktracking
    )]
    private static partial Regex IdPattern();

    public static IReadOnlyList<PluginSpec> Read(string directory, Action<string> report)
    {
        using var file = File.OpenRead(Path.Combine(directory, "plugins.json"));
        var selected = JsonSerializer.Deserialize<PluginSelection[]>(file, PipelineStore.JsonOptions)
            ?? throw new JsonException("The distribution must select an array of plugins.");
        var specs = new List<PluginSpec>();
        foreach (var group in selected.GroupBy(item => item?.Id, StringComparer.Ordinal))
        {
            var id = group.Key;
            if (id is null || !IdPattern().IsMatch(id))
            {
                report($"Invalid plugin ID '{id}'.");
                continue;
            }
            if (group.Count() > 1)
            {
                report($"Duplicate plugin ID '{id}'.");
                continue;
            }
            try
            {
                using var manifest = File.OpenRead(Path.Combine(directory, id, "plugin.json"));
                var spec = JsonSerializer.Deserialize<PluginSpec>(manifest, PipelineStore.JsonOptions);
                if (
                    spec is null
                    || !string.Equals(spec.Id, id, StringComparison.Ordinal)
                    || string.IsNullOrEmpty(spec.Assembly)
                    || string.IsNullOrEmpty(spec.EntryType)
                )
                    throw new JsonException("The manifest must declare the selected ID, entry assembly and type.");
                if (spec.Requires?.FirstOrDefault(dependency => !IdPattern().IsMatch(dependency.Id)) is { } invalid)
                    throw new JsonException($"Invalid required plugin ID '{invalid.Id}'.");
                specs.Add(spec with { Assembly = Path.Combine(id, spec.Assembly), Required = group.First().Required });
            }
            catch (Exception error) when (error is IOException or JsonException)
            {
                report($"Cannot load manifest for '{id}': {error.Message}");
            }
        }
        return specs;
    }
}
