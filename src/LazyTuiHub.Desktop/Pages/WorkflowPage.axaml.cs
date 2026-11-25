using System.Text.Json;
using Avalonia.Controls;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Desktop;

public sealed partial class WorkflowPage : UserControl, IWorkspaceView
{
    private readonly WorkflowOperations workflows;
    private readonly WorkflowPageState state;

    public WorkflowPage(WorkflowOperations workflows, WorkflowPageState state)
    {
        this.workflows = workflows;
        this.state = state;
        InitializeComponent();
        Pane.DetailOpen = state.DetailOpen;
        Pane.PropertyChanged += (_, change) =>
        {
            if (change.Property == AdaptivePane.DetailOpenProperty)
                state.DetailOpen = Pane.DetailOpen;
        };
        Reload();
        PlanTabs.SelectedIndex = state.Tab;
        PlanTabs.SelectionChanged += (_, _) => state.Tab = PlanTabs.SelectedIndex;
    }

    public Task RefreshAsync(IReadOnlySet<string> changedGroups)
    {
        if (changedGroups.Contains("workflow"))
            Reload();
        return Task.CompletedTask;
    }

    private void Reload()
    {
        var definitions = workflows.Saved();
        WorkflowCount.Text = $"{definitions.Count} saved workflows";
        EmptyWorkflows.IsVisible = definitions.Count == 0;
        Definitions.ItemsSource = definitions;
        PlanStatus.Text = "No saved workflows.";
        Definitions.SelectedItem = definitions.FirstOrDefault(item =>
            string.Equals(item.Name, state.Selected, StringComparison.Ordinal)
        ) ?? (definitions.Count == 0 ? null : definitions[0]);
    }

    private async void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Definitions.SelectedItem is not PipelineDefinition definition)
            return;
        state.Selected = definition.Name;
        WorkflowTitle.Text = definition.Name;
        PlanStatus.Text = "Planning…";
        Plan.Text = "";
        WorkflowDescription description;
        try
        {
            description = await workflows.Describe(definition.Name, CancellationToken.None);
        }
        catch (Exception error) when (error is AccessRequiredException or AccessDeniedException or KeyNotFoundException)
        {
            if (ReferenceEquals(Definitions.SelectedItem, definition))
                PlanStatus.Text = error.Message;
            return;
        }
        if (!ReferenceEquals(Definitions.SelectedItem, definition))
            return;
        Definition.Text = JsonSerializer.Serialize(definition, PipelineStore.JsonOptions);
        PlanStatus.Text = description.Executable ? "Ready to execute" : "Cannot execute";
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
        var plan = description.ExecutablePlan();
        Plan.Text =
            string.Join(
                Environment.NewLine,
                plan.Nodes.Select(node =>
                    $"{node.Definition.Id} · {node.Descriptor.Type} · {string.Join(" → ", node.Candidates)}"
                )
            )
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                plan.Connections.Select(edge => $"{edge.Source}.{edge.SourcePort} → {edge.Target}.{edge.TargetPort}")
            );
    }
}
