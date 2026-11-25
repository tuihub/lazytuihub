using LazyTuiHub.Sdk;

namespace LazyTuiHub.Tests;

public sealed class FailedActivationPlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration)
    {
        registration.RegisterNodes = (nodes, _) =>
            nodes.Register(new("failed.node", "failed", [], []), (_, _) => Task.CompletedTask);
        registration.MapEndpoints = _ => throw new InvalidOperationException("Deliberate activation failure.");
    }
}
