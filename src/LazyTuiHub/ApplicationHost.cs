using System.Diagnostics.CodeAnalysis;
using System.Net;
using Google.Protobuf.Reflection;
using Google.Rpc;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub;

public static class ApplicationHost
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The root DI factory owns the plugin runtime; startup failures dispose it explicitly."
    )]
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var path = builder.Configuration["database"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyTuiHub",
                "workspace.db"
            );
        var database = new SqliteDatabase(path);
        var keyDirectory = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "keys"));
        builder.Services.AddDataProtection().PersistKeysToFileSystem(keyDirectory).SetApplicationName("LazyTuiHub");
        builder
            .Services.AddGrpc()
            .AddJsonTranscoding(options => options.TypeRegistry = TypeRegistry.FromMessages(ErrorInfo.Descriptor));
        builder.Services.AddSingleton(_ => new HttpClient());
        var plugins = new PluginRuntime(
            database,
            builder.Configuration,
            builder.Configuration["plugin-directory"] ?? Path.Combine(AppContext.BaseDirectory, "plugins")
        );
        try
        {
            plugins.Configure(builder.Services);
            builder.Services.AddSingleton(_ => plugins);
            builder.Services.AddSingleton(plugins.Workflows);
            builder.Services.AddSingleton<IWorkflowExecutor>(plugins.Workflows);
            if (builder.Configuration.GetValue<bool>("demo"))
                builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            else if (builder.Configuration["command"] is null)
                builder.Services.AddHostedService<PluginTaskService>();
            var app = builder.Build();
            app.Services.GetRequiredService<PluginRuntime>().Activate(app);
            return app;
        }
        catch
        {
            plugins.Dispose();
            throw;
        }
    }
}
