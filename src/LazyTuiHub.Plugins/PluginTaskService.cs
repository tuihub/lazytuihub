using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LazyTuiHub.Plugins;

public sealed class PluginTaskService(PluginRuntime plugins, IServiceProvider services) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        plugins.MarkUnstartedTasksStopped();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        plugins.RunTasksAsync(services, stoppingToken);
}
