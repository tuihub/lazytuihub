namespace LazyTuiHub.Interaction;

public sealed class CapabilityCatalog(IEnumerable<CapabilityGroup> groups, Func<string, string>? unavailable = null)
{
    private readonly Dictionary<string, CapabilityGroup> byName = groups.ToDictionary(
        g => g.Name,
        StringComparer.Ordinal
    );
    public IEnumerable<CapabilityGroup> Groups =>
        byName.Values.Where(g => g.Available).OrderBy(g => g.Name, StringComparer.Ordinal);

    public CapabilityGroup Get(string name) =>
        byName.TryGetValue(name, out var group) && group.Available
            ? group
            : throw new InvalidOperationException(
                unavailable?.Invoke(name) ?? $"Operation group '{name}' is unavailable."
            );
}
