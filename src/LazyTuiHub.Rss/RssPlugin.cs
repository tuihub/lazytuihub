using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Api;
using LazyTuiHub.Plugins;
using LazyTuiHub.Rss.Application;
using LazyTuiHub.Rss.Commands;
using LazyTuiHub.Rss.Transport;

namespace LazyTuiHub.Rss;

public sealed class RssPlugin : IApplicationPlugin
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
        registration.Services.AddSingleton(_ => new RssStore(registration.Storage));
        registration.Services.AddSingleton(services => new RssOperations(
            services.GetRequiredService<IWorkflowExecutor>(),
            interval
        ));
        registration.Services.AddSingleton<IRssApi, RssApplication>();
        registration.Services.AddSingleton<RssRefreshWorker>();
        registration.DataTypes.Add("rss.feed");
        registration.DataTypes.Add("rss.article");
        registration.RegisterNodes = (nodes, services) =>
            RssModule.Register(nodes, services.GetRequiredService<RssStore>());
        foreach (var workflow in RssModule.Pipelines(interval))
            registration.Workflows.Add(workflow);
        registration.Tasks.Add(
            new("rss.refresh", ["rss.ingest"], (services, token) =>
                services.GetRequiredService<RssRefreshWorker>().RunAsync(token)
            )
        );
        registration.Commands.Add(new("rss", RssCommand.RunAsync));
        registration.MapEndpoints = endpoints => endpoints.MapGrpcService<RssService>();
    }
}
