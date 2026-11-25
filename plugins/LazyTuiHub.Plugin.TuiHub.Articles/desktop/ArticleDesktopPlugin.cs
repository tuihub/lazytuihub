using LazyTuiHub.Client;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Articles.Desktop;

public sealed class ArticleDesktopPlugin : IApplicationPlugin, IRemotePageProvider
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton(services => new ReaderModel(
            services.GetRequiredService<ArticleOperations>(),
            services.GetRequiredService<DesktopSession>(),
            services.GetRequiredService<ILogger<ReaderModel>>(),
            services.GetRequiredService<IPluginAvailability>().IsActive("lazytuihub.plugin.tuihub.rss")
                ? services.GetRequiredService<FeedOperations>()
                : null
        ));
        registration.AddDesktopPage(
            "reader",
            "Reader",
            services => new ReaderPage(services.GetRequiredService<ReaderModel>()),
            "ActionIconBook"
        );
    }

    public RemotePage RemotePage(RemoteWorkspace workspace)
    {
        return new("Reader", "ActionIconBook", provider => new ReaderPage(
            new ReaderModel(
                workspace.Articles,
                provider.GetRequiredService<DesktopSession>(),
                provider.GetRequiredService<ILogger<ReaderModel>>(),
                workspace.Feeds
            )
        ));
    }
}
