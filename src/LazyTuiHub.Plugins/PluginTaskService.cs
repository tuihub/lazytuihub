using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LazyTuiHub.Plugins;

public sealed class PluginTaskService(PluginRuntime plugins, IServiceProvider services) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        plugins.RunTasksAsync(services, stoppingToken);
}
