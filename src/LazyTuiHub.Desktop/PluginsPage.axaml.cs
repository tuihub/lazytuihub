using Avalonia.Controls;
using Avalonia.Interactivity;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Desktop;

public sealed partial class PluginsPage : UserControl
{
    private readonly PluginRuntime runtime;
    private readonly PluginsPageState state;
    private readonly ILogger<PluginsPage> logger;

    public PluginsPage(PluginRuntime runtime, PluginsPageState state, ILogger<PluginsPage> logger)
    {
        this.runtime = runtime;
        this.state = state;
        this.logger = logger;
        InitializeComponent();
        Pane.DetailOpen = state.DetailOpen;
        Pane.PropertyChanged += (_, change) =>
        {
            if (change.Property == AdaptivePane.DetailOpenProperty)
                state.DetailOpen = Pane.DetailOpen;
        };
        Reload(state.Selected);
        Status.Text = state.Status ?? runtime.DiscoveryError;
    }

    private void Reload(string? selected = null)
    {
        var plugins = runtime.List();
        PluginCount.Text = $"{plugins.Count} known plugins";
        Plugins.ItemsSource = plugins;
        Plugins.SelectedItem = plugins.FirstOrDefault(plugin =>
            string.Equals(plugin.Saved.Spec.Id, selected, StringComparison.Ordinal)
        ) ?? (plugins.Count == 0 ? null : plugins[0]);
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Plugins.SelectedItem is not PluginView plugin)
            return;
        state.Selected = plugin.Saved.Spec.Id;
        PluginTitle.Text = plugin.Saved.Spec.Name;
        PluginId.Text = plugin.Saved.Spec.Id;
        Activation.Text = plugin.Activation;
        Availability.Text = plugin.Availability;
        SavedPreference.Text = plugin.Saved.Enabled ? "Enabled" : "Disabled";
        Reason.Text = plugin.Reason;
        Reason.IsVisible = !string.IsNullOrEmpty(plugin.Reason);
        DeclaredPages.Text = string.Join(", ", plugin.Saved.Declaration.Pages.Select(page => page.Title));
        PageSection.IsVisible = plugin.Saved.Declaration.Pages.Count > 0;
        RequiredNotice.IsVisible = plugin.Saved.Spec.Required;
        RestartNotice.IsVisible = Toggle.IsVisible = Toggle.IsEnabled = !plugin.Saved.Spec.Required;
        Toggle.Content = plugin.Saved.Enabled ? "Disable after restart" : "Enable after restart";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not save plugin enablement.")]
    private static partial void LogSaveFailure(ILogger logger, Exception exception);

    private void ToggleClick(object? sender, RoutedEventArgs e)
    {
        if (Plugins.SelectedItem is not PluginView plugin)
            return;
        try
        {
            runtime.SetEnabled(plugin.Saved.Spec.Id, !plugin.Saved.Enabled);
            Reload(plugin.Saved.Spec.Id);
            Status.Text = state.Status = "Saved. Restart the application to apply this change.";
        }
        catch (Exception error)
        {
            Status.Text = state.Status = error.GetBaseException().Message;
            LogSaveFailure(logger, error);
        }
    }
}
