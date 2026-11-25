using System.Diagnostics.CodeAnalysis;
using System.Net;
using Google.Protobuf.Reflection;
using Google.Rpc;
using LazyTuiHub.Access;
using LazyTuiHub.Api;
using LazyTuiHub.Desktop;
using LazyTuiHub.Execution;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk.Avalonia;

namespace LazyTuiHub;

public static class ApplicationHost
{
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The returned host owns the runtime; failed startup disposes it here."
    )]
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder
            .Services.AddGrpc()
            .AddJsonTranscoding(options => options.TypeRegistry = TypeRegistry.FromMessages(ErrorInfo.Descriptor));
        var plugins = HostComposition.Configure(builder.Services, builder.Configuration);
        WebApplication? app = null;
        try
        {
            var worker = builder.Configuration.GetValue<bool>("worker");
            if (worker)
                builder.Services.AddSingleton<IWorkerApi, WorkerApplication>();
            if (builder.Configuration.GetValue<bool>("demo"))
                builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            else if (
                !worker
                && !builder.Configuration.GetValue<bool>("Access:Enabled")
                && builder.Configuration["command"] is null
            )
                builder.Services.AddHostedService<PluginTaskService>();
            app = builder.Build();
            app.Services.GetRequiredService<PluginRuntime>().Activate(app.Services, worker ? null : app);
            if (worker)
                WorkerEndpoint.Map(app);
            else if (builder.Configuration.GetValue<bool>("Access:Enabled"))
                AccessHosting.Map(app);
            return app;
        }
        catch
        {
            if (app is not null)
                ((IDisposable)app).Dispose();
            else
                plugins.Dispose();
            throw;
        }
    }

    public static IHost CreateDesktop(string[] args) => CreateInteractive(args, true);

    public static IHost CreateCli(string[] args) => CreateInteractive(args, false);

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The returned host owns the runtime; failed startup disposes it here."
    )]
    private static IHost CreateInteractive(string[] args, bool desktop)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var remote = builder.Configuration["endpoint"];
        var frontend = desktop ? FrontendKind.Gui : FrontendKind.Cli;
        var plugins = remote is null
            ? HostComposition.Configure(builder.Services, builder.Configuration, frontend)
            : null;
        LazyTuiHub.Client.RemoteWorkspace? connection = null;
        IHost? host = null;
        try
        {
            if (remote is not null)
            {
                connection = new(
                    remote,
                    builder.Configuration["Access:Session"],
                    builder.Configuration["Access:Account"]
                );
                RemoteDesktop.Configure(builder, connection, frontend);
            }
            if (desktop)
                ConfigureDesktop(builder, remote is null);
            host = builder.Build();
            if (connection is not null)
                _ = host.Services.GetRequiredService<LazyTuiHub.Client.RemoteWorkspace>();
            plugins?.Activate(host.Services);
            return host;
        }
        catch
        {
            if (host is not null)
                host.Dispose();
            else
            {
                plugins?.Dispose();
                connection?.Dispose();
            }
            throw;
        }
    }

    private static void ConfigureDesktop(HostApplicationBuilder builder, bool local)
    {
        builder.Services.AddSingleton<DesktopSession>();
        builder.Services.AddSingleton<RunDesktopOperation>(provider =>
            provider.GetRequiredService<DesktopSession>().RunAsync
        );
        builder.Services.AddSingleton(
            new AppearanceSettings(
                builder.Configuration["desktop-settings"]
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "LazyTuiHub",
                        "desktop.json"
                    )
            )
        );
        builder.Services.AddSingleton<DesktopAppearance>();
        builder.Services.AddSingleton<WorkflowPageState>();
        builder.Services.AddSingleton<PluginsPageState>();
        if (
            local
            && !builder.Configuration.GetValue<bool>("demo")
            && !builder.Configuration.GetValue<bool>("Access:Enabled")
        )
            builder.Services.AddHostedService<PluginTaskService>();
    }
}
