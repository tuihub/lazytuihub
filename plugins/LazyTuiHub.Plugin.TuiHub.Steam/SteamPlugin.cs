using LazyTuiHub.Plugin.TuiHub.Games;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Steam;

internal sealed class SteamPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        var worker = registration.Configuration.GetValue<bool>("worker");
        registration.Services.AddKeyedSingleton<HttpMessageHandler>(PluginProject.Id, (_, _) => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = worker,
                UseProxy = !worker,
            });
        registration.Services.AddSingleton<SteamStoreClient>();
        registration.Services.AddSingleton<GameAccountProvider, SteamAccount>();
        registration.Services.AddSingleton<IInstallationScanner, SteamInstallations>();
        registration.Services.AddSingleton<SteamCatalog>();
        registration.AddGameProvider("steam", services => services.GetRequiredService<SteamCatalog>());
    }
}
