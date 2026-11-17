using LazyTuiHub.Api;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Rss.Desktop;

public sealed class RssDesktopPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration) =>
        registration.AddDesktopPage("reader", "Reader", services => new ReaderPage(
            new ReaderModel(
                services.GetRequiredService<IRssApi>(),
                services.GetRequiredService<DesktopSession>(),
                services.GetRequiredService<ILogger<ReaderModel>>()
            )
        ));
}
