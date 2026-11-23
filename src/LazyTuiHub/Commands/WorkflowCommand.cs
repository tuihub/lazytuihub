using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Commands;

internal static class WorkflowCommand
{
    public static async Task RunAsync(WebApplication app, string action, CancellationToken token)
    {
        var workflows = app.Services.GetRequiredService<WorkflowRuntime>();
        var configuration = app.Configuration;
        switch (action)
        {
            case "help":
                Console.WriteLine(
                    "Workflow: nodes, list, definition/plan/run/watch --pipeline-name NAME [--pipeline-file FILE], save --pipeline-file FILE, relations --data-type TYPE --data-key KEY. --progress true reports execution events."
                );
                break;
            case "nodes":
                CommandJson.Write(
                    workflows.Registry.Descriptors.Select(node => new
                    {
                        node.Type,
                        node.Module,
                        node.RequiredResource,
                        Inputs = node.Inputs.Select(CommandJson.Port),
                        Outputs = node.Outputs.Select(CommandJson.Port),
                    })
                );
                break;
            case "list":
                CommandJson.Write(
                    await Task.WhenAll(
                        workflows.List().Select(definition => workflows.DescribeAsync(definition, token))
                    )
                );
                break;
            case "relations":
                var access = app.Services.GetRequiredService<ResourceAccess>();
                access.Require(ResourceUri.AccountRelations(access.Account), AccessMode.Read);
                CommandJson.Write(
                    workflows.Catalog.ReadRelations(
                        new(
                            CommandJson.Required(configuration, "data-type"),
                            CommandJson.Required(configuration, "data-key"),
                            access.Account
                        )
                    )
                );
                break;
            case "definition":
            case "save":
            case "plan":
            case "run":
            case "watch":
                await ExecuteAsync(workflows, configuration, action, token);
                break;
            default:
                throw new ArgumentException(
                    "Choose nodes, list, definition, save, plan, run, watch, or relations.",
                    nameof(action)
                );
        }
    }

    private static async Task ExecuteAsync(
        WorkflowRuntime workflows,
        IConfiguration configuration,
        string action,
        CancellationToken token
    )
    {
        var definition = configuration["pipeline-file"] is { } file
            ? JsonSerializer.Deserialize<PipelineDefinition>(
                await File.ReadAllTextAsync(file, token),
                PipelineStore.JsonOptions
            ) ?? throw new FormatException("The pipeline file is empty.")
            : workflows.Get(CommandJson.Required(configuration, "pipeline-name"));
        if (action is "definition")
        {
            CommandJson.Write(definition);
            return;
        }
        if (action is "save")
        {
            _ = CommandJson.Required(configuration, "pipeline-file");
            workflows.Save(definition);
            CommandJson.Write(await workflows.DescribeAsync(workflows.Get(definition.Name), token));
            return;
        }
        var description = await workflows.DescribeAsync(definition, token);
        if (action is "plan" && !description.Executable)
        {
            CommandJson.Write(description);
            Environment.ExitCode = 1;
            return;
        }
        var plan = description.ExecutablePlan();
        if (action is "plan")
        {
            CommandJson.Write(
                new
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
                        Inputs = node.Descriptor.Inputs.Select(CommandJson.Port),
                        Outputs = node.Descriptor.Outputs.Select(CommandJson.Port),
                    }),
                    plan.Connections,
                    UnavailablePeers = plan.Inventory.Unavailable,
                }
            );
            return;
        }
        await workflows.Runner.RunAsync(
            plan,
            new(
                Output: WritePacket,
                Continuous: action is "watch",
                Progress: configuration.GetValue<bool>("progress") ? WriteProgress : null
            ),
            token
        );
    }

    private static void WriteProgress(NodeProgress progress) =>
        Console.Error.WriteLine(JsonSerializer.Serialize(progress, PipelineStore.JsonOptions));

    private static ValueTask WritePacket(DataPacket packet, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CommandJson.Write(new { packet.Reference, packet.Payload });
        return ValueTask.CompletedTask;
    }
}
