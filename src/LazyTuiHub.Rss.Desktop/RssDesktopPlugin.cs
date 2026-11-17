using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Rss.Desktop;

public sealed class RssDesktopPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<ReaderModel>();
        registration.AddDesktopPage("reader", "Reader", services => new ReaderPage(
            services.GetRequiredService<ReaderModel>()
        ));
    }
}
