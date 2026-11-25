using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Plugins;

public sealed class PluginOperations(PluginRuntime plugins, ResourceAccess access, IServiceProvider services)
{
    public string? DiscoveryError => plugins.DiscoveryError;

    [Capability("List the saved and active state of workspace plugins.")]
    public IReadOnlyList<PluginView> Status()
    {
        access.Require(ResourceUri.SystemControl("operations"), AccessMode.Write);
        return plugins.List();
    }

    [Capability("List plugin status and tasks.")]
    public async Task<object> List(CancellationToken token) =>
        new
        {
            plugins.DiscoveryError,
            Plugins = Status(),
            Tasks = await plugins.TasksAsync(token),
        };

    [Capability("Inspect one plugin and its declarations.")]
    public PluginView Inspect(string plugin) =>
        Status().Single(p => p.Saved.Spec.Id.Equals(plugin, StringComparison.Ordinal));

    [Capability("Enable a plugin on the next start.")]
    public object Enable(string plugin) => SetEnabled(plugin, true);

    [Capability("Disable a plugin on the next start.")]
    public object Disable(string plugin) => SetEnabled(plugin, false);

    public object SetEnabled(string plugin, bool enabled)
    {
        plugins.SetEnabled(plugin, enabled);
        return new { Plugin = plugin, RestartRequired = true };
    }

    [Capability("Apply a JSON settings file on the next start.")]
    public async Task<object> Configure(string plugin, string file, CancellationToken token)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(file, token));
        plugins.SetSettings(plugin, settings.RootElement);
        return new { Plugin = plugin, RestartRequired = true };
    }

    [Capability("Run the plugin's cleanup hook.")]
    public Task<object> Cleanup(string plugin, CancellationToken token) => Hook(plugin, "cleanup", token);

    [Capability("Run the plugin's uninstall hook.")]
    public Task<object> Uninstall(string plugin, CancellationToken token) => Hook(plugin, "uninstall", token);

    private async Task<object> Hook(string plugin, string operation, CancellationToken token)
    {
        await plugins.InvokeHookAsync(plugin, operation, services, token);
        return new
        {
            Plugin = plugin,
            Hook = operation,
            Completed = true,
        };
    }
}
