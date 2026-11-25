using LazyTuiHub.Access;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Operations;

internal static class WorkspaceCapabilities
{
    private const string AccessGroup = "access";
    private const string PluginsGroup = "plugins";
    private const string WorkflowGroup = "workflow";
    private const string DaoGroup = "dao";
    public static IReadOnlyList<string> Groups { get; } = [AccessGroup, PluginsGroup, WorkflowGroup, DaoGroup];

    public static CapabilityCatalog Create(IServiceProvider services)
    {
        var plugins = services.GetRequiredService<PluginRuntime>();
        var access = services.GetRequiredService<ResourceAccess>();
        bool Manage() => access.Allows(AccountAuthority.OperationsUri, AccessMode.Write);
        var session = services.GetService<ClientSession>();
        var authority = services.GetService<AccountAuthority>();
        if (authority is not null && session is { Token: null })
            return new([CapabilityGroup.For(AccessGroup, () => new LocalSignIn(authority, session))], _ =>
                "A valid user session is required."
            );
        var groups = plugins.Capabilities.ToList();
        groups.Add(CapabilityGroup.For(PluginsGroup, services.GetRequiredService<PluginOperations>, Manage));
        groups.Add(CapabilityGroup.For(WorkflowGroup, services.GetRequiredService<WorkflowOperations>, Manage));
        groups.Add(CapabilityGroup.For(DaoGroup, () => new DaoOperations(plugins), Manage));
        if (authority is not null && session is not null)
            groups.Add(CapabilityGroup.For(AccessGroup, () => new AccessOperations(authority, session)));
        return new(groups, plugins.UnavailableGroup);
    }
}
