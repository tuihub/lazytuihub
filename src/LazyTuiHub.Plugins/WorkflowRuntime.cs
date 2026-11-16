using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

public sealed class WorkflowRuntime : IWorkflowExecutor
{
    private readonly PipelineStore pipelines;
    private readonly Dictionary<string, string> owners = new(StringComparer.Ordinal);
    private readonly Func<string, string?> retainedOwner;

    internal WorkflowRuntime(SqliteDatabase database, Func<string, string?> retainedOwner)
    {
        pipelines = new(database);
        Catalog = new(database);
        this.retainedOwner = retainedOwner;
        Runner = new(Registry);
    }

    public NodeRegistry Registry { get; } = new();
    public PipelineRunner Runner { get; }
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

    public WorkflowDescription Describe(PipelineDefinition definition)
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
        if (problems.Count == 0)
        {
            try
            {
                _ = Runner.CreatePlan(definition);
            }
            catch (ArgumentException error)
            {
                problems.Add(new(null, null, null, error.Message));
            }
        }
        return new(definition, problems);
    }

    public PipelinePlan CreatePlan(PipelineDefinition definition)
    {
        var description = Describe(definition);
        if (!description.Executable)
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    description.Problems.Select(problem =>
                        $"{problem.NodeId}: {problem.NodeType} (plugin {problem.PluginId ?? "unknown"}): {problem.Reason}"
                    )
                )
            );
        return Runner.CreatePlan(definition);
    }

    public Task RunAsync(string name, PipelineContext? context = null, CancellationToken cancellationToken = default) =>
        Runner.RunAsync(CreatePlan(Get(name)), context, cancellationToken);

    private string? Owner(string type) => owners.GetValueOrDefault(type) ?? retainedOwner(type);
}
