using LazyTuiHub.Api;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Articles.Desktop;

public sealed class ArticleDesktopPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton(services => new ReaderModel(
            services.GetRequiredService<IArticleApi>(),
            services.GetRequiredService<DesktopSession>(),
            services.GetRequiredService<ILogger<ReaderModel>>(),
            services.GetRequiredService<PluginRuntime>().IsActive("rss") ? services.GetRequiredService<IRssApi>() : null
        ));
        registration.AddDesktopPage("reader", "Reader", services => new ReaderPage(
            services.GetRequiredService<ReaderModel>()
        ));
    }
}
