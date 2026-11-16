using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Tests;

internal static class PluginTestHost
{
    public static WebApplication Create(
        string database,
        HttpClient http,
        string? directory = null,
        Dictionary<string, string?>? configuration = null
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(configuration ?? []);
        builder.Services.AddGrpc();
        builder
            .Services.AddDataProtection()
            .PersistKeysToFileSystem(new(Path.Combine(Path.GetDirectoryName(database)!, "keys")));
        builder.Services.AddSingleton(http);
        var plugins = new PluginRuntime(
            new SqliteDatabase(database),
            builder.Configuration,
            directory ?? Path.Combine(AppContext.BaseDirectory, "plugins")
        );
        plugins.Configure(builder.Services);
        builder.Services.AddSingleton(_ => plugins);
        builder.Services.AddSingleton(plugins.Workflows);
        builder.Services.AddSingleton(plugins.Retention);
        builder.Services.AddSingleton<IWorkflowExecutor>(plugins.Workflows);
        var app = builder.Build();
        app.Services.GetRequiredService<PluginRuntime>().Activate(app);
        return app;
    }
}
