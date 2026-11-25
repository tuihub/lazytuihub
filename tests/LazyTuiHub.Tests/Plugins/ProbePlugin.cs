using LazyTuiHub.Framework;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Tests;

public sealed class ProbePlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.Services.AddSingleton<ProbeState>();
        registration.RegisterNodes = (nodes, _) =>
            nodes.Register(new("probe.node", "probe", [], []), (_, _) => Task.CompletedTask);
        registration.Hooks = new(
            (services, _) =>
            {
                services.GetRequiredService<ProbeState>().Cleanups++;
                return Task.CompletedTask;
            },
            (services, _) =>
            {
                services.GetRequiredService<ProbeState>().Uninstalls++;
                return Task.CompletedTask;
            }
        );
        registration.Tasks.Add(new("probe.task", [], async (services, token) =>
            {
                var state = services.GetRequiredService<ProbeState>();
                state.Started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                finally
                {
                    state.Stopped.TrySetResult();
                }
            }));
    }
}
