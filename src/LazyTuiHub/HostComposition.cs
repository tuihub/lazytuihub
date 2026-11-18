using LazyTuiHub.Execution;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub;

internal static class HostComposition
{
    public static PluginRuntime Configure(IServiceCollection services, IConfiguration configuration)
    {
        var path = configuration["database"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyTuiHub",
                "workspace.db"
            );
        var keys = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "keys"));
        services.AddDataProtection().PersistKeysToFileSystem(keys).SetApplicationName("LazyTuiHub");
        ClusterConfiguration.Register(services, configuration);
        services.AddSingleton(_ =>
            configuration.GetValue<bool>("worker")
                ? new HttpClient(
                    new HttpClientHandler
                    {
                        CheckCertificateRevocationList = true,
                        AllowAutoRedirect = false,
                        UseProxy = false,
                    }
                )
                : new HttpClient()
        );
        var plugins = new PluginRuntime(
            new SqliteDatabase(path),
            configuration,
            configuration["plugin-directory"] ?? Path.Combine(AppContext.BaseDirectory, "plugins")
        );
        try
        {
            plugins.Configure(services);
            services.AddSingleton(_ => plugins);
            services.AddSingleton(plugins.Workflows);
            services.AddSingleton(plugins.Retention);
            services.AddSingleton<IWorkflowExecutor>(plugins.Workflows);
            return plugins;
        }
        catch
        {
            plugins.Dispose();
            throw;
        }
    }
}
