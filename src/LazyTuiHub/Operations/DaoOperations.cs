using LazyTuiHub.Dao;
using LazyTuiHub.Interaction;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Operations;

internal sealed class DaoOperations(PluginRuntime runtime)
{
    [Capability("Inspect storage providers, plugin requirements and retention.")]
    public object List() =>
        new
        {
            Providers = runtime.Daos.List(),
            Requirements = runtime
                .List()
                .Select(p => new
                {
                    Plugin = p.Saved.Spec.Id,
                    p.Saved.Declaration.DaoRequirements,
                    p.Activation,
                    p.Reason,
                }),
            Retention = runtime.Retention.List(),
        };

    [Capability("Clean expired records from explicitly delegated tables.")]
    public object Clean(CancellationToken token) => runtime.Retention.Clean(DateTimeOffset.UtcNow, token);

    [Capability("Pause retention for a table.")]
    public object Pause(string owner, string table) => SetRetention(owner, table, false);

    [Capability("Resume retention for a table.")]
    public object Resume(string owner, string table) => SetRetention(owner, table, true);

    private object SetRetention(string owner, string table, bool enabled)
    {
        runtime.Retention.SetEnabled(owner, new DaoTable(owner, table), enabled);
        return runtime.Retention.List();
    }
}
