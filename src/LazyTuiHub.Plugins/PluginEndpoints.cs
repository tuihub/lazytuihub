using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace LazyTuiHub.Plugins;

internal sealed class PluginEndpoints(IEndpointRouteBuilder parent) : IEndpointRouteBuilder
{
    public IServiceProvider ServiceProvider => parent.ServiceProvider;
    public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

    public IApplicationBuilder CreateApplicationBuilder() => parent.CreateApplicationBuilder();

    public void Commit()
    {
        foreach (var source in DataSources)
            parent.DataSources.Add(source);
    }
}
