using System.Text.Json;
using LazyTuiHub.Framework;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugins;

public sealed class PluginRegistration
{
    internal PluginRegistration(string id, JsonElement settings, IConfiguration configuration, BusinessStorage storage)
    {
        Id = id;
        Settings = settings;
        Configuration = configuration;
        Storage = storage;
    }

    public string Id { get; }
    public JsonElement Settings { get; }
    public IConfiguration Configuration { get; }
    public BusinessStorage Storage { get; }
    public IServiceCollection Services { get; } = new ServiceCollection();
    public ICollection<string> DataTypes { get; } = [];
    public ICollection<PipelineDefinition> Workflows { get; } = [];
    public ICollection<PluginTask> Tasks { get; } = [];
    public ICollection<PluginCommand> Commands { get; } = [];
    public Action<NodeRegistry, IServiceProvider>? RegisterNodes { get; set; }
    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
    public PluginHooks Hooks { get; set; } = new();
}
