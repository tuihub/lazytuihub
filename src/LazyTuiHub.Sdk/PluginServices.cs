using LazyTuiHub.Interaction;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Sdk;

public class PluginServices(string id, bool remote)
{
    public string Id { get; } = id;
    public bool Remote { get; } = remote;
    public IServiceCollection Services { get; } = new ServiceCollection();
    public ICollection<DeclaredPage> Pages { get; } = [];
    internal Dictionary<string, Func<IServiceProvider, CapabilityGroup>> Capabilities { get; } =
        new(StringComparer.Ordinal);

    public void AddCapabilities<T>(string name, Func<IServiceProvider, T>? factory = null)
        where T : class
    {
        if (factory is null)
            Services.AddTransient<T>();
        Capabilities.Add(name, services =>
            CapabilityGroup.For(name, () => factory is null ? services.GetRequiredService<T>() : factory(services))
        );
    }
}
