using System.Text.Json;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Commands;

internal static class PluginCommand
{
    public static async Task RunAsync(WebApplication app, string action, CancellationToken token)
    {
        var plugins = app.Services.GetRequiredService<PluginRuntime>();
        if (action is "help")
        {
            Console.WriteLine(
                "Plugins: list, inspect --plugin ID, enable/disable --plugin ID, configure --plugin ID --file FILE, cleanup/uninstall --plugin ID. Changes apply after restart; hooks do not remove DLLs."
            );
            return;
        }
        if (action is "list")
        {
            CommandJson.Write(
                new
                {
                    plugins.DiscoveryError,
                    Plugins = plugins.List(),
                    Tasks = await plugins.TasksAsync(token),
                }
            );
            return;
        }
        var id = CommandJson.Required(app.Configuration, "plugin");
        switch (action)
        {
            case "inspect":
                CommandJson.Write(
                    plugins.List().Single(item => string.Equals(item.Saved.Spec.Id, id, StringComparison.Ordinal))
                );
                break;
            case "enable":
            case "disable":
                plugins.SetEnabled(id, action is "enable");
                CommandJson.Write(new { Plugin = id, RestartRequired = true });
                break;
            case "configure":
                using (
                    var document = JsonDocument.Parse(
                        await File.ReadAllTextAsync(CommandJson.Required(app.Configuration, "file"), token)
                    )
                )
                    plugins.SetSettings(id, document.RootElement);
                CommandJson.Write(new { Plugin = id, RestartRequired = true });
                break;
            case "cleanup":
            case "uninstall":
                await plugins.InvokeHookAsync(id, action, app.Services, token);
                CommandJson.Write(
                    new
                    {
                        Plugin = id,
                        Hook = action,
                        Completed = true,
                    }
                );
                break;
            default:
                throw new ArgumentException(
                    "Choose list, inspect, enable, disable, configure, cleanup, or uninstall.",
                    nameof(action)
                );
        }
    }
}
