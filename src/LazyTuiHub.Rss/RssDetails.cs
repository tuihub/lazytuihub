using LazyTuiHub.Dao;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Rss;

public sealed class RssDetails
{
    private readonly IRecordDao records;
    private readonly DaoTable table;
    private readonly TimeSpan? retention;
    private readonly TimeProvider clock;

    public RssDetails(DaoScope daos, TimeSpan? retention, TimeProvider clock)
    {
        records = daos.Get<IRecordDao>("details");
        table = daos.Table("fetch_details");
        this.retention = retention;
        this.clock = clock;
        records.CreateTable(table);
    }

    public void Capture(HttpDocument document)
    {
        var now = clock.GetUtcNow();
        records.Put(
            table,
            Guid.NewGuid().ToString("D"),
            new FetchDetail(document.Uri.AbsoluteUri, document.Content.Length, now),
            retention is { } duration ? now + duration : null
        );
    }

    public IReadOnlyList<StoredRecord<FetchDetail>> Read(bool includeExpired = false) =>
        records.Read<FetchDetail>(table, clock.GetUtcNow(), includeExpired);

    public bool Keep(string key) => records.Keep(table, key);
}
