using LazyTuiHub.Framework;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugins;

public sealed class WorkflowRuntime : IWorkflowExecutor
{
    private readonly PipelineStore pipelines;
    private readonly Dictionary<string, string> owners = new(StringComparer.Ordinal);
    private readonly Func<string, string?> retainedOwner;
    private readonly ResourceAccess access;

    internal WorkflowRuntime(SqliteDatabase database, Func<string, string?> retainedOwner, ResourceAccess access)
    {
        pipelines = new(database);
        Catalog = new(database);
        this.retainedOwner = retainedOwner;
        this.access = access;
        Runner = new(Registry, access: access);
    }

    public NodeRegistry Registry { get; } = new();
    public PipelineRunner Runner { get; private set; }

    internal void PlaceOn(ExecutionTopology topology) => Runner = new(Registry, topology, access);

    public DataCatalog Catalog { get; }

    public IReadOnlyList<PipelineDefinition> List() => pipelines.List();

    public PipelineDefinition Get(string name) => pipelines.Get(name);

    internal void Register(string pluginId, NodeRegistry nodes)
    {
        Registry.Import(nodes);
        foreach (var descriptor in nodes.Descriptors)
            owners.Add(descriptor.Type, pluginId);
    }

    public void Save(PipelineDefinition definition, bool replace = true) =>
        pipelines.Save(
            definition with
            {
                Nodes = definition
                    .Nodes.Select(node => node with { PluginId = node.PluginId ?? Owner(node.Type) })
                    .ToArray(),
            },
            replace
        );

    public async Task<WorkflowDescription> DescribeAsync(PipelineDefinition definition, CancellationToken token)
    {
        var available = Registry.Descriptors.Select(node => node.Type).ToHashSet(StringComparer.Ordinal);
        var problems = new List<WorkflowProblem>();
        foreach (var node in definition.Nodes)
        {
            var owner = node.PluginId ?? Owner(node.Type);
            if (!available.Contains(node.Type))
                problems.Add(new(node.Id, node.Type, owner, "Node implementation is unavailable."));
            else if (
                node.PluginId is not null
                && !string.Equals(node.PluginId, owners[node.Type], StringComparison.Ordinal)
            )
                problems.Add(new(node.Id, node.Type, owner, "Loaded implementation belongs to a different plugin."));
        }
        PipelinePlan? plan = null;
        if (problems.Count == 0)
        {
            try
            {
                plan = await Runner.CreatePlanAsync(definition, token);
            }
            catch (Exception error)
                when (error is ArgumentException
                    || error is InvalidOperationException and not (AccessDeniedException or AccessRequiredException)
                )
            {
                problems.Add(new(null, null, null, error.Message));
            }
        }
        return new(definition, problems) { Plan = plan };
    }

    public async Task RunAsync(
        string name,
        PipelineContext? context = null,
        CancellationToken cancellationToken = default
    )
    {
        var description = await DescribeAsync(Get(name), cancellationToken);
        await Runner.RunAsync(description.ExecutablePlan(), context, cancellationToken);
    }

    private string? Owner(string type) => owners.GetValueOrDefault(type) ?? retainedOwner(type);
}
