using LazyTuiHub.Dao;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Tests;

public sealed class NativeOnlyStoragePlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration) =>
        registration.DaoServices.Add(
            new(new(DaoMode.Native, DaoContracts.Relational, "workspace", "sqlite"), registration.Workspace)
        );
}
