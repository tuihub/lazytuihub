using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games.Desktop;

public sealed class GameDesktopPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<GamePageModel>();
        registration.Services.AddSingleton<GameCovers>();
        registration.AddDesktopPage("library", "Games", services => new GamePage(
            services.GetRequiredService<GamePageModel>(),
            services.GetRequiredService<GameCovers>()
        ));
    }
}
