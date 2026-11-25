using LazyTuiHub.Dao;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Tests;

public sealed class NativeOnlyStoragePlugin : IApplicationPlugin
{
    public void Configure(PluginRegistration registration) =>
        registration.DaoServices.Add(
            new(new(DaoMode.Native, DaoContracts.Relational, "workspace", "sqlite"), registration.Workspace)
        );
}
