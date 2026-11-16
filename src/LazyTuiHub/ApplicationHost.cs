using System.Net;
using Google.Protobuf.Reflection;
using Google.Rpc;
using LazyTuiHub.Api;
using LazyTuiHub.Application;
using LazyTuiHub.Framework;
using LazyTuiHub.Rss;
using LazyTuiHub.Transport;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub;

public static class ApplicationHost
{
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var database = builder.Configuration["database"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LazyTuiHub",
                "workspace.db"
            );
        var keyDirectory = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database))!, "keys"));
        builder.Services.AddDataProtection().PersistKeysToFileSystem(keyDirectory).SetApplicationName("LazyTuiHub");
        builder
            .Services.AddGrpc()
            .AddJsonTranscoding(options => options.TypeRegistry = TypeRegistry.FromMessages(ErrorInfo.Descriptor));
        builder.Services.AddSingleton<IGreetingApi, GreetingApplication>();
        builder.Services.AddSingleton<IRssApi, RssApplication>();
        builder.Services.AddSingleton(_ => new SqliteDatabase(database));
        builder.Services.AddSingleton(_ => new HttpClient());
        builder.Services.AddSingleton(services => new RssWorkspace(
            services.GetRequiredService<SqliteDatabase>(),
            services.GetRequiredService<HttpClient>(),
            builder.Configuration.GetValue<TimeSpan?>("refresh-interval") ?? TimeSpan.FromMinutes(15)
        ));
        if (builder.Configuration.GetValue<bool>("demo"))
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        else if (builder.Configuration["rss"] is null)
            builder.Services.AddHostedService<RssRefreshWorker>();
        var app = builder.Build();
        app.MapGrpcService<GreetingService>();
        app.MapGrpcService<RssService>();
        return app;
    }
}
