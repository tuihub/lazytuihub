using LazyTuiHub.Api;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.CorePlugin;

public sealed class CoreLibraryPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<IGreetingApi, GreetingApplication>();
        registration.RegisterNodes = (nodes, services) =>
            BuiltInNodes.Register(
                nodes,
                services.GetRequiredService<HttpClient>(),
                registration.Configuration.GetValue<bool>("worker")
                    ? (registration.Configuration.GetSection("Cluster:FetchOrigins").Get<string[]>() ?? [])
                        .Select(value => new Uri(value).GetLeftPart(UriPartial.Authority))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : null
            );
        registration.MapEndpoints = endpoints => endpoints.MapGrpcService<GreetingService>();
    }
}
