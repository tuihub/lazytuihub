using System.Reflection;

namespace LazyTuiHub.Interaction;

public sealed class CapabilityGroup
{
    private readonly Func<bool> available;
    private readonly Dictionary<string, Capability> byName;
    public string Name { get; }
    public IReadOnlyList<Capability> Actions { get; }
    public bool Available => available();

    private CapabilityGroup(string name, Type type, Func<object> resolve, Func<bool>? available)
    {
        Name = name;
        this.available = available ?? (() => true);
        byName = type.GetMethods()
            .Where(m => m.IsDefined(typeof(CapabilityAttribute)))
            .Select(method => new Capability(this, method, resolve))
            .ToDictionary(a => a.Name, StringComparer.Ordinal);
        Actions = byName.Values.OrderBy(a => a.Name, StringComparer.Ordinal).ToArray();
    }

    public Capability Get(string name) =>
        byName.TryGetValue(name, out var operation)
            ? operation
            : throw new ArgumentException($"Unknown {Name} action '{name}'.", nameof(name));

    public static CapabilityGroup For<T>(string name, Func<T> resolve, Func<bool>? available = null)
        where T : class => new(name, typeof(T), resolve, available);
}
