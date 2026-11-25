using System.Text.Json;
using LazyTuiHub.Client;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;

namespace LazyTuiHub;

internal static partial class RemoteDesktop
{
    public static void Configure(HostApplicationBuilder builder, string endpoint)
    {
        var config = builder.Configuration;
        builder.Services.AddSingleton(_ => new RemoteWorkspace(
            endpoint,
            config["Access:Session"],
            config["Access:Account"]
        ));
        builder.Services.AddSingleton(s => s.GetRequiredService<RemoteWorkspace>().Catalog);
        builder.Services.AddSingleton(s => s.GetRequiredService<RemoteWorkspace>().Session);
        builder.Services.AddSingleton<PluginAssemblies>();
        builder.Services.AddSingleton<IEnumerable<RemotePage>>(services =>
            LoadPages(services, config["plugin-directory"] ?? Path.Combine(AppContext.BaseDirectory, "plugins"))
        );
    }

    private static List<RemotePage> LoadPages(IServiceProvider services, string folder)
    {
        var pages = new List<RemotePage>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(RemoteDesktop));
        try
        {
            var specs = PluginManifest.Read(folder, message => LogInvalid(logger, message));
            foreach (var spec in specs.Where(s => s.RemotePage))
            {
                try
                {
                    var entry = services
                        .GetRequiredService<PluginAssemblies>()
                        .Load(Path.Combine(folder, spec.Assembly), spec.EntryType);
                    if (entry is IRemotePageProvider provider)
                        pages.Add(provider.RemotePage(services.GetRequiredService<RemoteWorkspace>()));
                }
                catch (Exception error)
                {
                    LogMissing(logger, spec.Id, error);
                }
            }
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            LogMissing(logger, "plugins.json", error);
        }
        return pages;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load remote presentation {Plugin}.")]
    private static partial void LogMissing(ILogger logger, string plugin, Exception error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    private static partial void LogInvalid(ILogger logger, string problem);
}
