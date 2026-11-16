using LazyTuiHub.Dao;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Commands;

internal static class DaoCommand
{
    public static void Run(WebApplication app, string action, CancellationToken token)
    {
        var runtime = app.Services.GetRequiredService<PluginRuntime>();
        switch (action)
        {
            case "help":
                Console.WriteLine(
                    "DAO: list, clean, pause/resume --owner ID --table NAME. Cleanup only processes explicitly delegated targets."
                );
                break;
            case "list":
                CommandJson.Write(
                    new
                    {
                        Providers = runtime.Daos.List(),
                        Requirements = runtime
                            .List()
                            .Select(plugin => new
                            {
                                Plugin = plugin.Saved.Spec.Id,
                                plugin.Saved.Declaration.DaoRequirements,
                                plugin.Activation,
                                plugin.Reason,
                            }),
                        Retention = runtime.Retention.List(),
                    }
                );
                break;
            case "clean":
                CommandJson.Write(runtime.Retention.Clean(DateTimeOffset.UtcNow, token));
                break;
            case "pause":
            case "resume":
                var owner = CommandJson.Required(app.Configuration, "owner");
                var table = new DaoTable(owner, CommandJson.Required(app.Configuration, "table"));
                runtime.Retention.SetEnabled(owner, table, action is "resume");
                CommandJson.Write(runtime.Retention.List());
                break;
            default:
                throw new ArgumentException("Choose list, clean, pause, or resume.", nameof(action));
        }
    }
}
