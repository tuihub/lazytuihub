namespace LazyTuiHub.Plugins;

public sealed record PluginSpec(
    string Id,
    string Name,
    string Assembly,
    PluginEntryPoints EntryPoints,
    bool Required = false,
    PluginDependency[]? Requires = null,
    string? Version = null,
    PluginDependency[]? CompatibleWith = null
)
{
    internal IEnumerable<string> RequiredIds => (Requires ?? []).Select(dependency => dependency.Id);

    internal IEnumerable<PluginDependency> Relations => (Requires ?? []).Concat(CompatibleWith ?? []);

    internal IEnumerable<string> Problems(Func<string, PluginSpec?> available)
    {
        if (Version is not null && !NuGet.Versioning.NuGetVersion.TryParse(Version, out _))
            yield return $"Invalid declared version '{Version}'.";
        foreach (var dependency in Requires ?? [])
            if (dependency.Problem(available(dependency.Id), optional: false) is { } problem)
                yield return problem;
        foreach (var peer in CompatibleWith ?? [])
            if (peer.Problem(available(peer.Id), optional: true) is { } mismatch)
                yield return mismatch;
    }
}
