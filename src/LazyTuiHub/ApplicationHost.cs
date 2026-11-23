using System.Diagnostics.CodeAnalysis;
using System.Net;
using Google.Protobuf.Reflection;
using Google.Rpc;
using LazyTuiHub.Access;
using LazyTuiHub.Api;
using LazyTuiHub.Desktop;
using LazyTuiHub.Execution;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

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

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The returned host owns the runtime; failed startup disposes it here."
    )]
    public static IHost CreateDesktop(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var plugins = HostComposition.Configure(builder.Services, builder.Configuration, desktop: true);
        IHost? host = null;
        try
        {
            builder.Services.AddSingleton<DesktopSession>();
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
                !builder.Configuration.GetValue<bool>("demo") && !builder.Configuration.GetValue<bool>("Access:Enabled")
            )
                builder.Services.AddHostedService<PluginTaskService>();
            host = builder.Build();
            host.Services.GetRequiredService<PluginRuntime>().Activate(host.Services);
            return host;
        }
        catch
        {
            if (host is not null)
                host.Dispose();
            else
                plugins.Dispose();
            throw;
        }
    }

    internal static IDisposable? EnterLocal(IServiceProvider services, IConfiguration configuration)
    {
        var access = services.GetRequiredService<ResourceAccess>();
        if (access.IsSingleAccount)
            return null;
        var token = configuration["Access:Session"] ?? throw new AccessRequiredException();
        return access.Enter(
            services.GetRequiredService<AccountAuthority>().Authenticate(token, configuration["Access:Account"])
        );
    }
}
