using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed partial class GameLibrary
{
    private const string SyncColumn = "synced";
    private const string ConnectionsResource = "connections";
    private const string OriginId = "$origin";
    private const string EvidenceId = "$evidence";
    private readonly string origins;
    private readonly string holdings;
    private readonly string overrides;

    public bool CanReadConnections =>
        access.Allows(ResourceUri.InAccount(PluginProject.Id, access.Account, ConnectionsResource), AccessMode.Read);
    public bool CanManageConnections =>
        access.Allows(
            ResourceUri.InAccount(PluginProject.Id, access.Account, ConnectionsResource),
            AccessMode.Read | AccessMode.Write
        );

    internal void RequireOrigins(AccessMode mode) =>
        access.Require(ResourceUri.InAccount(PluginProject.Id, access.Account, ConnectionsResource), mode);

    public IReadOnlyList<GameOrigin> Origins()
    {
        RequireOrigins(AccessMode.Read);
        using var connection = database.Open();
        using var cmd = Command(
            connection,
            null,
            $"SELECT id,provider,external,label,local,connected,synced,revision FROM {origins} WHERE account=$account ORDER BY local,label"
        );
        using var rows = cmd.ExecuteReader();
        var output = new List<GameOrigin>();
        while (rows.Read())
            output.Add(
                new(
                    rows.GetString("id"),
                    rows.GetString("provider"),
                    rows.GetString("external"),
                    rows.GetString("label"),
                    rows.GetBoolean("local"),
                    rows.GetBoolean("connected"),
                    rows.IsDBNull(SyncColumn) ? null : rows.GetString(SyncColumn),
                    rows.GetInt64("revision")
                )
            );
        return output;
    }

    internal GameOrigin Connect(string provider, PlatformIdentity identity, bool local, string credential)
    {
        RequireOrigins(AccessMode.Read | AccessMode.Write);
        using var connection = database.Open();
        using var cmd = Command(
            connection,
            null,
            $"""
            INSERT INTO {origins}(account,id,provider,external,label,local,connected,credential,revision)
            VALUES ($account,$id,$provider,$external,$label,$local,1,$secret,1)
            ON CONFLICT(account,provider,external,local) DO UPDATE SET
                label=excluded.label,connected=1,credential=excluded.credential,revision=revision+1
            RETURNING id,revision,synced
            """,
            (GameId, Guid.NewGuid().ToString("N")),
            ("$provider", provider),
            ("$external", identity.Id),
            ("$label", identity.Name),
            ("$local", local),
            ("$secret", credential)
        );
        using var row = cmd.ExecuteReader();
        row.Read();
        return new(
            row.GetString("id"),
            provider,
            identity.Id,
            identity.Name,
            local,
            true,
            row.IsDBNull(SyncColumn) ? null : row.GetString(SyncColumn),
            row.GetInt64("revision")
        );
    }

    internal string Credential(GameOrigin origin)
    {
        RequireOrigins(AccessMode.Read | AccessMode.Write);
        using var connection = database.Open();
        using var cmd = Command(
            connection,
            null,
            $"SELECT credential FROM {origins} WHERE account=$account AND id=$id AND connected=1 AND revision=$revision",
            (GameId, origin.Id),
            ("$revision", origin.Revision)
        );
        return cmd.ExecuteScalar() as string
            ?? throw new InvalidOperationException("Reconnect this account before synchronizing.");
    }

    public void Disconnect(string id)
    {
        RequireOrigins(AccessMode.Write);
        using var connection = database.Open();
        using var cmd = Command(
            connection,
            null,
            $"UPDATE {origins} SET connected=0,credential=NULL,revision=revision+1 WHERE account=$account AND id=$id",
            (GameId, id)
        );
        if (cmd.ExecuteNonQuery() == 0)
            throw new KeyNotFoundException("Connection not found.");
    }

    internal int ReplaceInventory(GameOrigin origin, IReadOnlyList<PlatformGame> rows, CancellationToken token)
    {
        RequireOrigins(AccessMode.Read | AccessMode.Write);
        access.Require(
            ResourceUri.InAccount(PluginProject.Id, access.Account, Collection),
            AccessMode.Read | AccessMode.Write
        );
        return storage.Write(database, (connection, transaction) =>
        {
            using var stamp = Command(
                connection,
                transaction,
                $"UPDATE {origins} SET synced=$time,revision=revision+1 WHERE account=$account AND id=$id AND revision=$revision AND connected=1",
                (GameId, origin.Id),
                ("$revision", origin.Revision),
                ("$time", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
            );
            if (stamp.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("The connection changed during sync. Refresh and try again.");
            using var clear = Command(
                connection,
                transaction,
                $"UPDATE {holdings} SET present=0 WHERE account=$account AND origin=$origin",
                (OriginId, origin.Id)
            );
            clear.ExecuteNonQuery();
            var registered = new List<DataReference>();
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                var game = ResolveInventoryGame(connection, transaction, origin, row, registered);
                using var save = Command(
                    connection,
                    transaction,
                    $"""
                    INSERT INTO {holdings}(account,origin,evidence,external,game,status,path,present) VALUES ($account,$origin,$evidence,$external,$id,$status,$path,1)
                    ON CONFLICT(account,origin,evidence) DO UPDATE SET game=excluded.game,status=excluded.status,path=excluded.path,present=1
                    """,
                    (OriginId, origin.Id),
                    ("$external", row.Id),
                    (EvidenceId, row.Path ?? row.Id),
                    (GameId, game),
                    ("$status", row.Status),
                    ("$path", row.Path)
                );
                save.ExecuteNonQuery();
            }
            token.ThrowIfCancellationRequested();
            return new BusinessChange<int>(rows.Count, registered, []);
        });
    }

    private string ResolveInventoryGame(
        DbConnection connection,
        DbTransaction tx,
        GameOrigin origin,
        PlatformGame item,
        List<DataReference> registered
    )
    {
        // An explicit previous assignment survives disappearance and reappearance.
        using var prior = Command(
            connection,
            tx,
            $"SELECT game FROM {holdings} WHERE account=$account AND origin=$origin AND evidence=$evidence",
            (OriginId, origin.Id),
            (EvidenceId, item.Path ?? item.Id)
        );
        if (prior.ExecuteScalar() is string assigned)
            return assigned;
        var key = new CatalogKey(origin.Provider, item.Id);
        var match = index.Match(key);
        var gamesFound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in match.Unique ? match.Candidates : [key])
        {
            using var find = Command(
                connection,
                tx,
                $"""
                SELECT game FROM {sources} WHERE account=$account AND provider=$provider AND external_id=$external
                UNION SELECT h.game FROM {holdings} h JOIN {origins} o ON o.account=h.account AND o.id=h.origin
                WHERE h.account=$account AND o.provider=$provider AND h.external=$external
                """,
                ("$provider", candidate.Provider),
                ("$external", candidate.Id)
            );
            using var reader = find.ExecuteReader();
            while (reader.Read())
                gamesFound.Add(reader.GetString(0));
        }
        if (gamesFound.Count == 1)
            return gamesFound.Single();
        var entry = Insert(connection, tx, item.Title);
        registered.Add(Reference(entry.Id));
        return entry.Id;
    }

    public void Assign(string origin, string evidence, string game) => MoveObservation(origin, evidence, game, null);

    public GameEntry Split(string origin, string evidence, string title) =>
        MoveObservation(origin, evidence, null, title);

    private GameEntry MoveObservation(string origin, string evidence, string? target, string? title)
    {
        RequireOrigins(AccessMode.Read | AccessMode.Write);
        return storage.Write(database, (db, tx) =>
        {
            using var previous = Command(
                db,
                tx,
                $"SELECT game FROM {holdings} WHERE account=$account AND origin=$origin AND evidence=$evidence",
                (OriginId, origin),
                (EvidenceId, evidence)
            );
            if (previous.ExecuteScalar() is not string from)
                throw new KeyNotFoundException("Library observation not found.");
            Require(from, AccessMode.Read | AccessMode.Write);
            var entry = target is not null ? Get(db, tx, target, AccessMode.Write) : Insert(db, tx, title!);
            using var cmd = Command(
                db,
                tx,
                $"UPDATE {holdings} SET game=$id WHERE account=$account AND origin=$origin AND evidence=$evidence",
                (GameId, entry.Id),
                (OriginId, origin),
                (EvidenceId, evidence)
            );
            cmd.ExecuteNonQuery();
            return new BusinessChange<GameEntry>(entry, target is null ? [Reference(entry.Id)] : [], []);
        });
    }

    public IReadOnlyList<GameShelfItem> Shelf()
    {
        var entries = List();
        var counts = new Dictionary<string, (int Accounts, int Installs)>(StringComparer.Ordinal);
        if (
            access.Allows(ResourceUri.InAccount(PluginProject.Id, access.Account, ConnectionsResource), AccessMode.Read)
        )
        {
            using var db = database.Open();
            using var cmd = Command(
                db,
                null,
                $"""
                SELECT h.game,count(DISTINCT CASE WHEN NOT o.local THEN o.id END) AS accounts,sum(o.local) AS installs
                FROM {holdings} h JOIN {origins} o ON o.account=h.account AND o.id=h.origin
                WHERE h.account=$account AND h.present=1 GROUP BY h.game
                """
            );
            using var rows = cmd.ExecuteReader();
            while (rows.Read())
                counts.Add(rows.GetString("game"), (rows.GetInt32("accounts"), rows.GetInt32("installs")));
        }
        using var connection = database.Open();
        var snapshots = new Dictionary<string, List<GameMetadata>>(StringComparer.Ordinal);
        using (
            var cmd = Command(
                connection,
                null,
                $"""
                SELECT game,json_remove(metadata,'$.Description','$.Publishers') FROM {sources}
                WHERE account=$account ORDER BY game,provider,external_id
                """
            )
        )
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
            {
                var id = reader.GetString(0);
                if (!snapshots.TryGetValue(id, out var list))
                    snapshots[id] = list = [];
                list.Add(JsonSerializer.Deserialize<GameMetadata>(reader.GetString(1))!);
            }
        var personal = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        using (
            var cmd = Command(
                connection,
                null,
                $"SELECT game,field,value FROM {overrides} WHERE account=$account AND field IN ('Cover','Released','Developers','Other title')"
            )
        )
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
            {
                var id = reader.GetString("game");
                if (!personal.TryGetValue(id, out var fields))
                    personal[id] = fields = new(StringComparer.Ordinal);
                fields.Add(reader.GetString("field"), reader.GetString("value"));
            }
        return entries
            .Select(entry =>
            {
                var count = counts.GetValueOrDefault(entry.Id);
                var records = snapshots.GetValueOrDefault(entry.Id) ?? [];
                var detail = new GameDetails(
                    entry,
                    records,
                    [],
                    [],
                    personal.GetValueOrDefault(entry.Id) ?? new(StringComparer.Ordinal)
                );
                var fields = detail.Fields.ToDictionary(f => f.Name, f => f.Value, StringComparer.Ordinal);
                var aliases = records
                    .SelectMany(r => (r.OtherTitles ?? []).Append(r.Title).Append(r.AlternativeTitle))
                    .Append(fields.GetValueOrDefault("Other title"))
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new GameShelfItem(
                    entry.Id,
                    entry.Title,
                    count.Accounts,
                    count.Installs,
                    fields.GetValueOrDefault("Cover")
                )
                {
                    Released = fields.GetValueOrDefault("Released"),
                    Developers = fields.GetValueOrDefault("Developers"),
                    Aliases = aliases,
                    Categories = entry.Categories,
                    Providers = records.Select(r => r.Provider).Distinct(StringComparer.Ordinal).ToArray(),
                };
            })
            .ToArray();
    }

    private List<GameAvailability> Availability(DbConnection db, DbTransaction tx, string id)
    {
        var list = new List<GameAvailability>();
        if (
            !access.Allows(
                ResourceUri.InAccount(PluginProject.Id, access.Account, ConnectionsResource),
                AccessMode.Read
            )
        )
            return list;
        using var cmd = Command(
            db,
            tx,
            $"""
            SELECT o.id,o.provider,CASE WHEN o.local THEN substr(o.external,1,instr(o.external,'/')-1) ELSE o.label END AS label,
                   h.external,h.status,h.path,o.connected FROM {holdings} h
            JOIN {origins} o ON o.account=h.account AND o.id=h.origin
            WHERE h.account=$account AND h.game=$id AND h.present=1 ORDER BY o.local DESC,o.label,h.external
            """,
            (GameId, id)
        );
        using var rows = cmd.ExecuteReader();
        while (rows.Read())
            list.Add(
                new(
                    rows.GetString("id"),
                    rows.GetString("provider"),
                    rows.GetString("label"),
                    rows.GetString("external"),
                    rows.GetString("status") + (rows.GetBoolean("connected") ? string.Empty : " (disconnected)"),
                    rows.IsDBNull("path") ? null : rows.GetString("path")
                )
            );
        return list;
    }
}
