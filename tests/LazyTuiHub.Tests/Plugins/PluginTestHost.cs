using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk;
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
        Dictionary<string, string?>? configuration = null,
        ResourceAccess? access = null,
        Action<IServiceCollection>? configure = null,
        IReadOnlyList<PluginSpec>? specs = null,
        FrontendKind frontend = FrontendKind.None
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
            directory ?? Path.Combine(AppContext.BaseDirectory, "plugins"),
            access,
            frontend: frontend
        );
        if (specs is null)
            plugins.Configure(builder.Services);
        else
            plugins.Configure(builder.Services, specs);
        builder.Services.AddSingleton(_ => plugins);
        builder.Services.AddSingleton(plugins.Workflows);
        builder.Services.AddSingleton(plugins.Retention);
        builder.Services.AddSingleton<IWorkflowExecutor>(plugins.Workflows);
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        app.Services.GetRequiredService<PluginRuntime>().Activate(app.Services, app);
        return app;
    }
}
