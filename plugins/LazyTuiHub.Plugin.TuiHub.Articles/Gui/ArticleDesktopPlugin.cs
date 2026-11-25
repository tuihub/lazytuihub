using LazyTuiHub.Plugin.TuiHub.Articles.Frontend;
using LazyTuiHub.Sdk;
using LazyTuiHub.Sdk.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Plugin.TuiHub.Articles.Desktop;

internal sealed class ArticleDesktopPlugin : IPluginFrontend
{
    public void Configure(PluginServices registration)
    {
        registration.Services.AddSingleton(services => new ReaderModel(
            services.GetRequiredService<ArticleOperations>(),
            services.GetRequiredService<RunDesktopOperation>(),
            services.GetRequiredService<ILogger<ReaderModel>>(),
            services.GetService<Func<IArticleSources?>>()?.Invoke()
        ));
        registration.AddDesktopPage(
            "reader",
            "Reader",
            services => new ReaderPage(services.GetRequiredService<ReaderModel>()),
            "ActionIconBook"
        );
    }
}
