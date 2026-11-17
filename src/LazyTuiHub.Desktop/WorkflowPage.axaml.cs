using System.Text.Json;
using Avalonia.Controls;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Desktop;

public sealed partial class WorkflowPage : UserControl
{
    private readonly WorkflowRuntime workflows;

    public WorkflowPage(WorkflowRuntime workflows)
    {
        this.workflows = workflows;
        InitializeComponent();
        Definitions.ItemsSource = workflows.List();
        PlanStatus.Text = "No saved workflows.";
        Definitions.SelectedIndex = 0;
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Definitions.SelectedItem is not PipelineDefinition definition)
            return;
        var description = workflows.Describe(definition);
        Definition.Text = JsonSerializer.Serialize(definition, PipelineStore.JsonOptions);
        PlanStatus.Text = description.Executable ? "Ready to execute locally" : "Cannot execute";
        if (!description.Executable)
        {
            Plan.Text = string.Join(
                Environment.NewLine,
                description.Problems.Select(problem =>
                    $"{problem.NodeId}: {problem.NodeType} · plugin {problem.PluginId ?? "unknown"} · {problem.Reason}"
                )
            );
            return;
        }
        var plan = workflows.CreatePlan(definition);
        Plan.Text =
            string.Join(
                Environment.NewLine,
                plan.Nodes.Select(node => $"{node.Definition.Id} · {node.Descriptor.Type} · {node.Instance}")
            )
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                plan.Connections.Select(edge => $"{edge.Source}.{edge.SourcePort} → {edge.Target}.{edge.TargetPort}")
            );
    }
}
