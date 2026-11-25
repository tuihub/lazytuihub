using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Sdk;

public sealed class PluginRegistration : PluginServices
{
    internal PluginRegistration(
        string id,
        JsonElement settings,
        IConfiguration configuration,
        BusinessStorage storage,
        IConnectionSource workspace,
        DaoRegistry daos
    )
        : base(id, false)
    {
        Settings = settings;
        Configuration = configuration;
        Storage = storage;
        Workspace = workspace;
        Daos = new(id, daos);
    }

    public JsonElement Settings { get; }
    public IConfiguration Configuration { get; }
    public BusinessStorage Storage { get; }
    public IConnectionSource Workspace { get; }
    public DaoScope Daos { get; }
    public ICollection<DaoService> DaoServices { get; } = new List<DaoService>();
    public ICollection<RetentionDelegation> Retentions { get; } = new List<RetentionDelegation>();
    public ICollection<string> DataTypes { get; } = [];
    public ICollection<PipelineDefinition> Workflows { get; } = [];
    public ICollection<PluginTask> Tasks { get; } = [];
    public Action<NodeRegistration, IServiceProvider>? RegisterNodes { get; set; }
    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
    public PluginHooks Hooks { get; set; } = new();
}
