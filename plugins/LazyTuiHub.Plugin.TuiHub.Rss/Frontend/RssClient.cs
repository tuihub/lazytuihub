using LazyTuiHub.Plugin.TuiHub.Articles;
using LazyTuiHub.Plugin.TuiHub.Rss.Api;
using LazyTuiHub.Plugin.TuiHub.Rss.Frontend;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed class RssClient : IPluginFrontend
{
    public void Configure(PluginServices registration)
    {
        registration.Services.AddTransient<Func<IArticleSources?>>(services =>
            () =>
                registration.Remote || services.GetRequiredService<IPluginAvailability>().IsActive(registration.Id)
                    ? new RssArticleSources(services.GetRequiredService<FeedOperations>())
                    : null
        );
        if (registration.Remote)
        {
            registration.Services.AddTransient<IRssApi, RssHttpClient>();
            registration.AddCapabilities<FeedOperations>("rss");
        }
    }
}
