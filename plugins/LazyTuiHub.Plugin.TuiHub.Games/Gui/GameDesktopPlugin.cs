using LazyTuiHub.Sdk;
using LazyTuiHub.Sdk.Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Games.Desktop;

internal sealed class GameDesktopPlugin : IPluginFrontend
{
    public void Configure(PluginServices registration)
    {
        if (registration.Remote)
            return;
        registration.Services.AddSingleton<GamePageModel>();
        registration.Services.AddSingleton<GameCovers>();
        registration.AddDesktopPage<GamePage>("library", "Games", "ActionIconGames");
    }
}
