using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Plugins;

public sealed class WorkflowOperations(WorkflowRuntime workflows, ResourceAccess access)
{
    [Capability("List saved workflow definitions.")]
    public IReadOnlyList<PipelineDefinition> Saved()
    {
        access.Require(ResourceUri.SystemControl("operations"), AccessMode.Write);
        return workflows.List();
    }

    [Capability("Inspect readiness and planning problems for a workflow.")]
    public Task<WorkflowDescription> Describe(string pipelineName, CancellationToken token)
    {
        access.Require(ResourceUri.SystemControl("operations"), AccessMode.Write);
        return workflows.DescribeAsync(workflows.Get(pipelineName), token);
    }

    [Capability("Inspect registered workflow node types and ports.")]
    public object Nodes() =>
        workflows.Registry.Descriptors.Select(node => new
        {
            node.Type,
            node.Module,
            node.RequiredResource,
            Inputs = node.Inputs.Select(Port),
            Outputs = node.Outputs.Select(Port),
        });

    private static object Port(PortDescriptor port) =>
        new
        {
            port.Name,
            Type = port.DataType.FullName,
            Delivery = port.Delivery.ToString(),
        };

    [Capability("List saved workflows and their availability.")]
    public Task<WorkflowDescription[]> List(CancellationToken token) =>
        Task.WhenAll(workflows.List().Select(d => workflows.DescribeAsync(d, token)));

    [Capability("Read the data relations for this account and key.")]
    public object Relations(string dataType, string dataKey)
    {
        access.Require(ResourceUri.AccountRelations(access.Account), AccessMode.Read);
        return workflows.Catalog.ReadRelations(new(dataType, dataKey, access.Account));
    }

    [Capability("Open a saved workflow by name or load a JSON definition from a file.")]
    public async Task<PipelineDefinition> Definition(
        CancellationToken token,
        string? pipelineName = null,
        string? pipelineFile = null
    ) =>
        pipelineFile is not null
            ? JsonSerializer.Deserialize<PipelineDefinition>(
                await File.ReadAllTextAsync(pipelineFile, token),
                PipelineStore.JsonOptions
            ) ?? throw new FormatException("The pipeline file is empty.")
            : workflows.Get(
                pipelineName ?? throw new ArgumentException("Provide a pipeline name or file.", nameof(pipelineName))
            );

    [Capability("Save a JSON workflow definition to the workspace.")]
    public async Task<WorkflowDescription> Save(string pipelineFile, CancellationToken token)
    {
        var definition = await Definition(token, pipelineFile: pipelineFile);
        workflows.Save(definition);
        return await workflows.DescribeAsync(workflows.Get(definition.Name), token);
    }

    [Capability("Build an execution plan for a named workflow or a file.")]
    public async Task<object> Plan(CancellationToken token, string? pipelineName = null, string? pipelineFile = null)
    {
        var description = await workflows.DescribeAsync(await Definition(token, pipelineName, pipelineFile), token);
        if (!description.Executable)
            return new UnsuccessfulResult(description);
        var plan = description.ExecutablePlan();
        return new
        {
            plan.Name,
            plan.BufferCapacity,
            Nodes = plan.Nodes.Select(node => new
            {
                node.Definition.Id,
                node.Definition.Type,
                node.Definition.PluginId,
                node.Instance,
                node.Candidates,
                node.ExecutionMode,
                node.Requirements,
                Inputs = node.Descriptor.Inputs.Select(Port),
                Outputs = node.Descriptor.Outputs.Select(Port),
            }),
            plan.Connections,
            UnavailablePeers = plan.Inventory.Unavailable,
        };
    }

    [Capability("Run a workflow once and stream its output.")]
    public Task Run(
        InteractionOutput output,
        CancellationToken token,
        string? pipelineName = null,
        string? pipelineFile = null,
        bool progress = false
    ) => Execute(false, output, pipelineName, pipelineFile, progress, token);

    [Capability("Run a workflow continuously until cancelled.")]
    public Task Watch(
        InteractionOutput output,
        CancellationToken token,
        string? pipelineName = null,
        string? pipelineFile = null,
        bool progress = false
    ) => Execute(true, output, pipelineName, pipelineFile, progress, token);

    private async Task Execute(
        bool continuous,
        InteractionOutput output,
        string? name,
        string? file,
        bool progress,
        CancellationToken token
    )
    {
        var plan = (await workflows.DescribeAsync(await Definition(token, name, file), token)).ExecutablePlan();
        await workflows.Runner.RunAsync(
            plan,
            new(
                Output: (packet, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    output.Write(new { packet.Reference, packet.Payload });
                    return ValueTask.CompletedTask;
                },
                Continuous: continuous,
                Progress: progress ? p => output.Notify(JsonSerializer.Serialize(p, PipelineStore.JsonOptions)) : null
            ),
            token
        );
    }
}
