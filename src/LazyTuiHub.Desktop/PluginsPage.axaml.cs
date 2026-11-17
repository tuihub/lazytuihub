using Avalonia.Controls;
using Avalonia.Interactivity;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Desktop;

public sealed partial class PluginsPage : UserControl
{
    private readonly PluginRuntime runtime;
    private readonly ILogger<PluginsPage> logger;

    public PluginsPage(PluginRuntime runtime, ILogger<PluginsPage> logger)
    {
        this.runtime = runtime;
        this.logger = logger;
        InitializeComponent();
        Reload();
        Status.Text = runtime.DiscoveryError;
    }

    private void Reload(string? selected = null)
    {
        var plugins = runtime.List();
        Plugins.ItemsSource = plugins;
        Plugins.SelectedItem = plugins.FirstOrDefault(plugin =>
            string.Equals(plugin.Saved.Spec.Id, selected, StringComparison.Ordinal)
        ) ?? (plugins.Count == 0 ? null : plugins[0]);
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Plugins.SelectedItem is not PluginView plugin)
            return;
        Toggle.IsEnabled = !plugin.Saved.Spec.Required;
        var action = plugin.Saved.Enabled ? "Disable after restart" : "Enable after restart";
        Toggle.Content = plugin.Saved.Spec.Required ? "Required plugin" : action;
        Details.Text =
            $"{plugin.Saved.Spec.Id}\nSaved preference: {(plugin.Saved.Enabled ? "enabled" : "disabled")}\nThis startup: {plugin.Activation}\nFiles: {plugin.Availability}\n{plugin.Reason}\n\nPages: {string.Join(", ", plugin.Saved.Declaration.Pages.Select(page => page.Title))}";
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
            Status.Text = "Saved. Restart the application to apply this change.";
        }
        catch (Exception error)
        {
            Status.Text = error.GetBaseException().Message;
            LogSaveFailure(logger, error);
        }
    }
}
