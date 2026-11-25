using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugins;

public sealed class PluginRegistration
{
    internal PluginRegistration(
        string id,
        JsonElement settings,
        IConfiguration configuration,
        BusinessStorage storage,
        IConnectionSource workspace,
        DaoRegistry daos
    )
    {
        Id = id;
        Settings = settings;
        Configuration = configuration;
        Storage = storage;
        Workspace = workspace;
        Daos = new(id, daos);
    }

    public string Id { get; }
    public JsonElement Settings { get; }
    public IConfiguration Configuration { get; }
    public BusinessStorage Storage { get; }
    public IConnectionSource Workspace { get; }
    public DaoScope Daos { get; }
    public ICollection<DaoService> DaoServices { get; } = new List<DaoService>();
    public ICollection<RetentionDelegation> Retentions { get; } = new List<RetentionDelegation>();
    public IServiceCollection Services { get; } = new ServiceCollection();
    public ICollection<string> DataTypes { get; } = [];
    public ICollection<PipelineDefinition> Workflows { get; } = [];
    public ICollection<PluginTask> Tasks { get; } = [];
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

    public ICollection<DeclaredPage> Pages { get; } = [];
    public Action<NodeRegistry, IServiceProvider>? RegisterNodes { get; set; }
    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
    public PluginHooks Hooks { get; set; } = new();
}
