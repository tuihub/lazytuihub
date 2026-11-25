using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

public sealed class RetentionRuntime : IRetentionCleanup
{
    private readonly DaoRegistry daos;
    private readonly RetentionCatalog catalog;

    internal RetentionRuntime(SqliteDatabase database, DaoRegistry daos)
    {
        this.daos = daos;
        catalog = new(database);
    }

    internal void Observe(PluginRegistration registration)
    {
        foreach (var delegation in registration.Retentions)
        {
            var requirement = registration.Daos.Requirements.Single(item =>
                string.Equals(item.Name, delegation.Dao, StringComparison.Ordinal)
            );
            if (
                requirement.Mode != DaoMode.Unified
                || !string.Equals(requirement.Contract, DaoContracts.Records, StringComparison.Ordinal)
            )
                throw new ArgumentException("Retention requires the unified records contract.", nameof(registration));
            catalog.Observe(registration.Id, requirement, delegation.Table);
        }
    }

    public IReadOnlyList<RetentionView> List() => catalog.Read().Select(Describe).ToArray();

    private RetentionView Describe(RetentionRecord record)
    {
        if (!record.Enabled)
            return new(record, "paused", null);
        if (daos.Problem(record.Requirement) is { } problem)
            return new(record, "unavailable", problem);
        return new(record, record.LastError is null ? "ready" : "failed", record.LastError);
    }

    public void SetEnabled(string owner, DaoTable table, bool enabled) => catalog.SetEnabled(owner, table, enabled);

    void IRetentionCleanup.Clean(DateTimeOffset now, CancellationToken token) => Clean(now, token);

    public IReadOnlyList<RetentionView> Clean(DateTimeOffset now, CancellationToken token = default)
    {
        foreach (var record in catalog.Read())
        {
            token.ThrowIfCancellationRequested();
            if (!record.Enabled || daos.Problem(record.Requirement) is not null)
                continue;
            int deleted;
            try
            {
                deleted = daos.Resolve<IRecordDao>(record.Requirement).DeleteExpired(record.Table, now);
            }
            catch (Exception error)
            {
                catalog.Result(record, now, 0, error.Message);
                continue;
            }
            catalog.Result(record, now, deleted, null);
        }
        return List();
    }
}
