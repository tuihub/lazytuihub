using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Rss.Api;
using LazyTuiHub.Plugin.TuiHub.Rss.Application;
using LazyTuiHub.Plugin.TuiHub.Rss.Frontend;
using LazyTuiHub.Plugin.TuiHub.Rss.Transport;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed class RssPlugin : IApplicationPlugin
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    public void Configure(PluginRegistration registration)
    {
        var interval =
            registration.Settings.ValueKind == JsonValueKind.Object
            && registration.Settings.TryGetProperty("refreshInterval", out var setting)
                ? TimeSpan.Parse(setting.GetString()!, CultureInfo.InvariantCulture)
                : registration.Configuration.GetValue<TimeSpan?>("refresh-interval") ?? DefaultInterval;
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(registration), "Refresh interval must be positive.");
        registration.Daos.Requirements.Add(new("database", DaoMode.Native, DaoContracts.Relational, Backend: "sqlite"));
        registration.Daos.Requirements.Add(new("details", DaoMode.Unified, DaoContracts.Records));
        registration.Retentions.Add(new("details", registration.Daos.Table("fetch_details")));
        var retention =
            registration.Settings.TryGetProperty("detailRetention", out var duration)
            && duration.ValueKind != JsonValueKind.Null
                ? TimeSpan.Parse(duration.GetString()!, CultureInfo.InvariantCulture)
                : (TimeSpan?)null;
        if (retention is { } configured && configured <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(registration), "Detail retention must be positive.");
        registration.Services.AddSingleton(services => new RssStore(
            registration.Daos,
            registration.Storage,
            services.GetRequiredService<ResourceAccess>()
        ));
        registration.Services.AddSingleton(services => new RssDetails(
            registration.Daos,
            retention,
            TimeProvider.System,
            services.GetRequiredService<ResourceAccess>()
        ));
        registration.Services.AddSingleton(services => new RssOperations(
            services.GetRequiredService<IWorkflowExecutor>(),
            interval
        ));
        registration.Services.AddSingleton<IRssApi, RssApplication>();
        registration.Services.AddSingleton<RssRefreshWorker>();
        registration.DataTypes.Add("rss.feed");
        registration.RegisterNodes = (nodes, services) =>
            RssModule.Register(
                nodes,
                services.GetRequiredService<RssStore>(),
                services.GetRequiredService<RssDetails>()
            );
        foreach (var workflow in RssModule.Pipelines(interval))
            registration.Workflows.Add(workflow);
        registration.Tasks.Add(
            new("rss.refresh", ["rss.ingest"], (services, token) =>
                services.GetRequiredService<RssRefreshWorker>().RunAsync(token)
            )
        );
        registration.AddCapabilities<LocalFeedOperations>("rss");
        registration.Services.AddTransient<FeedOperations>(services =>
            services.GetRequiredService<LocalFeedOperations>()
        );
        registration.MapEndpoints = endpoints => endpoints.MapGrpcService<RssService>();
    }
}
