using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed class RssDetails
{
    private readonly IRecordDao records;
    private readonly DaoTable table;
    private readonly TimeSpan? retention;
    private readonly TimeProvider clock;
    private readonly ResourceAccess access;
    private string KeyPrefix => access.Account + "/";

    public RssDetails(DaoScope daos, TimeSpan? retention, TimeProvider clock, ResourceAccess? access = null)
    {
        records = daos.Get<IRecordDao>("details");
        table = daos.Table("fetch_details");
        this.retention = retention;
        this.clock = clock;
        this.access = access ?? new(singleAccount: true);
        records.CreateTable(table);
    }

    public void Capture(HttpDocument document)
    {
        var now = clock.GetUtcNow();
        records.Put(
            table,
            KeyPrefix + Guid.NewGuid().ToString("D"),
            new FetchDetail(document.Uri.AbsoluteUri, document.Content.Length, now),
            retention is { } duration ? now + duration : null
        );
    }

    public IReadOnlyList<StoredRecord<FetchDetail>> Read(bool includeExpired = false)
    {
        access.Require(ResourceUri.InAccount(PluginProject.Id, access.Account, "details"), AccessMode.Read);
        return records
            .Read<FetchDetail>(table, clock.GetUtcNow(), includeExpired)
            .Where(item => item.Key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            .ToArray();
    }

    public bool Keep(string key)
    {
        access.Require(ResourceUri.InAccount(PluginProject.Id, access.Account, "details"), AccessMode.Write);
        return key.StartsWith(KeyPrefix, StringComparison.Ordinal) && records.Keep(table, key);
    }
}
