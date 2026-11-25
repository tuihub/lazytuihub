using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Games;

public static class GameProviderRegistration
{
    private const string Search = "search";
    private const string Lookup = "lookup";

    public static void AddGameProvider(
        this PluginRegistration registration,
        string provider,
        Func<IServiceProvider, IGameCatalog> resolve
    )
    {
        registration.DataTypes.Add(provider + ".game");
        registration.RegisterNodes += (nodes, services) =>
        {
            var catalog = resolve(services);
            Register<GameMetadata[]>(nodes, provider, Search, catalog.SearchAsync);
            Register<GameMetadata>(nodes, provider, Lookup, catalog.LookupAsync);
        };
        foreach (var action in new[] { Search, Lookup, "import" })
            registration.Workflows.Add(Workflow(provider, registration.Id, action));
        registration.AddCapabilities(provider, services => new ProviderOperations(
            provider,
            services.GetRequiredService<IWorkflowExecutor>()
        ));
    }

    private static void Register<T>(
        NodeRegistration nodes,
        string id,
        string action,
        Func<string, CancellationToken, Task<T>> execute
    )
        where T : notnull =>
        nodes.RegisterRepeatable<string, T>(
            new(
                id + "." + action,
                id,
                [PortDescriptor.Stream<string>(StreamNodes.Input)],
                [PortDescriptor.Stream<T>(StreamNodes.Output)]
            ),
            (input, _, token) => execute(input, token)
        );

    private static PipelineDefinition Workflow(string provider, string pluginId, string action)
    {
        var nodes = new List<PipelineNode>
        {
            PipelineNode.Create("query", "games.query", pluginId: PluginProject.Id),
            PipelineNode.Create("provider", provider + "." + (action is Search ? Search : Lookup), pluginId: pluginId),
        };
        if (action is "import")
        {
            nodes.Add(PipelineNode.Create("prepare", "games.prepare-import", pluginId: PluginProject.Id));
            nodes.Add(PipelineNode.Create("store", "games.store", pluginId: PluginProject.Id));
        }
        nodes.Add(PipelineNode.Create("output", $"games.{action}-output", pluginId: PluginProject.Id));
        return StreamWorkflow.Chain(provider + "." + action, nodes);
    }
}
