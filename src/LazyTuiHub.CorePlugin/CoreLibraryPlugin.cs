using LazyTuiHub.Api;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.CorePlugin;

public sealed class CoreLibraryPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<IGreetingApi, GreetingApplication>();
        registration.RegisterNodes = (nodes, services) =>
            BuiltInNodes.Register(nodes, services.GetRequiredService<HttpClient>());
        registration.MapEndpoints = endpoints => endpoints.MapGrpcService<GreetingService>();
    }
}
