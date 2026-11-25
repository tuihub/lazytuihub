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
                var selection = group.First();
                if (
                    string.IsNullOrWhiteSpace(selection.Assembly)
                    || !string.Equals(
                        Path.GetFileName(selection.Assembly),
                        selection.Assembly,
                        StringComparison.Ordinal
                    )
                )
                    throw new JsonException("The selection must name the plugin DLL.");
                var assembly = Path.Combine(id, selection.Assembly);
                var path = Path.Combine(directory, assembly);
                if (!File.Exists(path))
                {
                    specs.Add(new(id, id, assembly, new(), selection.Required));
                    continue;
                }
                var spec = EmbeddedManifest.Read(path);
                if (!string.Equals(spec.Id, id, StringComparison.Ordinal) || spec.EntryPoints is null)
                    throw new JsonException("The embedded manifest must declare the selected ID and its entry points.");
                if (spec.Relations.FirstOrDefault(dependency => !IdPattern().IsMatch(dependency.Id)) is { } invalid)
                    throw new JsonException($"Invalid related plugin ID '{invalid.Id}'.");
                specs.Add(spec with { Assembly = assembly, Required = group.First().Required });
            }
            catch (Exception error)
                when (error is IOException or JsonException or BadImageFormatException or OverflowException)
            {
                report($"Cannot load manifest for '{id}': {error.Message}");
            }
        }
        return specs;
    }
}
