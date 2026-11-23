using LazyTuiHub.Access;
using LazyTuiHub.Execution;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub;

internal static class HostComposition
{
    public static PluginRuntime Configure(
        IServiceCollection services,
        IConfiguration configuration,
        bool desktop = false
    )
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
        var database = new SqliteDatabase(path);
        var enabled = configuration.GetValue<bool>("Access:Enabled");
        var authority =
            enabled && !configuration.GetValue<bool>("worker")
                ? new AccountAuthority(database, configuration, TimeProvider.System)
                : null;
        if (authority is not null)
            services.AddSingleton(authority);
        var principal =
            desktop && authority is not null
                ? authority.Authenticate(
                    configuration["Access:Session"] ?? throw new AccessRequiredException(),
                    configuration["Access:Account"]
                )
                : null;
        var plugins = new PluginRuntime(
            database,
            configuration,
            configuration["plugin-directory"] ?? Path.Combine(AppContext.BaseDirectory, "plugins"),
            new ResourceAccess(singleAccount: !enabled, principal)
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
