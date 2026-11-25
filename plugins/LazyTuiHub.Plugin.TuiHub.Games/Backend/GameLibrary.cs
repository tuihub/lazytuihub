using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugin.TuiHub.Games;

[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Statements interpolate only encoded DAO table names. All values use parameters."
)]
[SuppressMessage(
    "Major Code Smell",
    "S2077:Formatting SQL queries is security-sensitive",
    Justification = "DaoNames encodes table names and the command binds record values."
)]
internal sealed partial class GameLibrary
{
    private const string Collection = GamesPlugin.Group;
    private const string GameId = "$id";
    private readonly IConnectionSource database;
    private readonly BusinessStorage storage;
    private readonly ResourceAccess access;
    private readonly GameIdIndex index;
    private readonly string games;
    private readonly string sources;
    private readonly string sessions;

    public GameLibrary(DaoScope daos, BusinessStorage storage, ResourceAccess access, GameIdIndex index)
    {
        database = daos.Get<IConnectionSource>("database");
        this.storage = storage;
        this.access = access;
        this.index = index;
        games = DaoNames.Table(daos.Table("library"));
        sources = DaoNames.Table(daos.Table("sources"));
        origins = DaoNames.Table(daos.Table("connections"));
        holdings = DaoNames.Table(daos.Table("inventory"));
        overrides = DaoNames.Table(daos.Table("overrides"));
        sessions = DaoNames.Table(daos.Table("sessions"));
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {games} (
                account TEXT NOT NULL, id TEXT NOT NULL, title TEXT NOT NULL, categories TEXT NOT NULL DEFAULT '[]',
                PRIMARY KEY(account, id)
            );
            CREATE TABLE IF NOT EXISTS {sources} (
                account TEXT NOT NULL, provider TEXT NOT NULL, external_id TEXT NOT NULL, game TEXT NOT NULL, metadata TEXT NOT NULL,
                PRIMARY KEY(account, provider, external_id, game), FOREIGN KEY(account, game) REFERENCES {games}(account, id)
            );
            CREATE TABLE IF NOT EXISTS {origins} (
                account TEXT NOT NULL, id TEXT NOT NULL, provider TEXT NOT NULL, external TEXT NOT NULL, label TEXT NOT NULL,
                local INTEGER NOT NULL, connected INTEGER NOT NULL, credential TEXT, synced TEXT, revision INTEGER NOT NULL,
                PRIMARY KEY(account,id), UNIQUE(account,provider,external,local)
            );
            CREATE TABLE IF NOT EXISTS {holdings} (
                account TEXT NOT NULL, origin TEXT NOT NULL, evidence TEXT NOT NULL, external TEXT NOT NULL, game TEXT NOT NULL, status TEXT NOT NULL, path TEXT, present INTEGER NOT NULL,
                PRIMARY KEY(account,origin,evidence), FOREIGN KEY(account,game) REFERENCES {games}(account,id),
                FOREIGN KEY(account,origin) REFERENCES {origins}(account,id)
            );
            CREATE INDEX IF NOT EXISTS {DaoNames.Index(
                daos.Table("inventory"),
                "external"
            )} ON {holdings}(account,external);
            CREATE TABLE IF NOT EXISTS {overrides} (
                account TEXT NOT NULL, game TEXT NOT NULL, field TEXT NOT NULL, value TEXT NOT NULL,
                PRIMARY KEY(account,game,field), FOREIGN KEY(account,game) REFERENCES {games}(account,id)
            );
            CREATE TABLE IF NOT EXISTS {sessions} (
                account TEXT NOT NULL, game TEXT NOT NULL, session TEXT NOT NULL,
                FOREIGN KEY(account, game) REFERENCES {games}(account, id)
            );
            """;
        command.ExecuteNonQuery();
    }

    public bool CanAddGames =>
        access.Allows(
            ResourceUri.InAccount(PluginProject.Id, access.Account, Collection),
            AccessMode.Read | AccessMode.Write
        );

    public bool CanEditGame(string id) =>
        access.Allows(
            ResourceUri.InAccount(PluginProject.Id, access.Account, Collection, id),
            AccessMode.Read | AccessMode.Write
        );

    public GameEntry Add(string title) => storage.Write(database, (connection, transaction) =>
        {
            var entry = Insert(connection, transaction, title);
            return new BusinessChange<GameEntry>(entry, [Reference(entry.Id)], []);
        });

    public GameEntry Import(GameMetadata metadata, string? target = null)
    {
        if (string.IsNullOrWhiteSpace(metadata.Provider) || string.IsNullOrWhiteSpace(metadata.ExternalId))
            throw new ArgumentException("Source metadata needs a provider and an external ID.", nameof(metadata));
        return storage.Write(database, (connection, transaction) =>
        {
            var id = target ?? FindSingleBinding(connection, transaction, metadata);
            var entry = id is null
                ? Insert(connection, transaction, metadata.Title)
                : Get(connection, transaction, id, AccessMode.Write);
            using var save = Command(
                connection,
                transaction,
                $"""
                INSERT INTO {sources}(account,provider,external_id,game,metadata) VALUES ($account,$provider,$external,$id,$metadata)
                ON CONFLICT(account,provider,external_id,game) DO UPDATE SET metadata=excluded.metadata
                """,
                ("$provider", metadata.Provider),
                ("$external", metadata.ExternalId),
                (GameId, entry.Id),
                ("$metadata", JsonSerializer.Serialize(metadata))
            );
            save.ExecuteNonQuery();
            var source = new DataReference(metadata.Provider + ".game", metadata.ExternalId, access.Account);
            var game = Reference(entry.Id);
            return new BusinessChange<GameEntry>(entry, [source, game], [new(source, game, "describes")]);
        });
    }

    private string? FindSingleBinding(DbConnection connection, DbTransaction transaction, GameMetadata metadata)
    {
        access.Require(
            ResourceUri.InAccount(PluginProject.Id, access.Account, Collection),
            AccessMode.Read | AccessMode.Write
        );
        using var command = Command(
            connection,
            transaction,
            $"""
            SELECT game FROM {sources} WHERE account=$account AND provider=$provider AND external_id=$external
            UNION SELECT h.game FROM {holdings} h JOIN {origins} o ON o.id=h.origin AND o.account=h.account
            WHERE $canRead AND h.account=$account AND o.provider=$provider AND h.external=$external
            """,
            ("$canRead", CanReadConnections),
            ("$provider", metadata.Provider),
            ("$external", metadata.ExternalId)
        );
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        var id = reader.GetString(0);
        if (reader.Read())
            throw new InvalidOperationException("This source describes multiple games. Choose one with --game.");
        return id;
    }

    public IReadOnlyList<GameEntry> List()
    {
        var visible = access.ReadableChildren(ResourceUri.InAccount(PluginProject.Id, access.Account, Collection));
        using var connection = database.Open();
        using var command = Command(
            connection,
            null,
            $"""
            SELECT id,title,categories FROM {games} WHERE account=$account
            AND ($ids IS NULL OR id IN (SELECT value FROM json_each($ids))) ORDER BY title,id
            """,
            ("$ids", visible is null ? null : JsonSerializer.Serialize(visible))
        );
        using var reader = command.ExecuteReader();
        var entries = new List<GameEntry>();
        while (reader.Read())
            entries.Add(ReadEntry(reader));
        return entries;
    }

    public GameDetails Show(string id)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var entry = Get(connection, transaction, id, AccessMode.Read);
        return new(
            entry,
            ReadJson<GameMetadata>(
                connection,
                transaction,
                id,
                $"SELECT metadata FROM {sources} WHERE account=$account AND game=$id ORDER BY provider,external_id"
            ),
            ReadJson<GameSession>(
                connection,
                transaction,
                id,
                $"SELECT session FROM {sessions} WHERE account=$account AND game=$id ORDER BY rowid"
            ),
            Availability(connection, transaction, id),
            ReadOverrides(connection, transaction, id)
        );
    }

    public GameEntry Edit(string id, string? title, string? addCategory, string? removeCategory)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var entry = Get(connection, transaction, id, AccessMode.Write);
        var categories = entry.Categories.ToHashSet(StringComparer.Ordinal);
        if (addCategory is not null)
            categories.Add(addCategory);
        if (removeCategory is not null)
            categories.Remove(removeCategory);
        var edited = entry with
        {
            Title = title ?? entry.Title,
            Categories = categories.Order(StringComparer.Ordinal).ToArray(),
        };
        if (title is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
        SaveEntry(connection, transaction, edited);
        transaction.Commit();
        return edited;
    }

    private void SaveEntry(DbConnection connection, DbTransaction transaction, GameEntry edited)
    {
        using var command = Command(
            connection,
            transaction,
            $"UPDATE {games} SET title=$title,categories=$categories WHERE account=$account AND id=$id",
            (GameId, edited.Id),
            ("$title", edited.Title),
            ("$categories", JsonSerializer.Serialize(edited.Categories))
        );
        command.ExecuteNonQuery();
    }

    public void LogSession(string id, GameSession session)
    {
        if (string.IsNullOrWhiteSpace(session.Device) || session.Duration < TimeSpan.Zero)
            throw new ArgumentException("A play session needs a device and a nonnegative duration.", nameof(session));
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        _ = Get(connection, transaction, id, AccessMode.Write);
        using var command = Command(
            connection,
            transaction,
            $"INSERT INTO {sessions}(account,game,session) VALUES ($account,$id,$session)",
            (GameId, id),
            ("$session", JsonSerializer.Serialize(session))
        );
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private GameEntry Insert(DbConnection connection, DbTransaction transaction, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        access.Require(
            ResourceUri.InAccount(PluginProject.Id, access.Account, Collection),
            AccessMode.Read | AccessMode.Write
        );
        var id = Guid.NewGuid().ToString("N");
        using var command = Command(
            connection,
            transaction,
            $"INSERT INTO {games}(account,id,title) VALUES ($account,$id,$title)",
            (GameId, id),
            ("$title", title)
        );
        command.ExecuteNonQuery();
        return new(id, title, []);
    }

    private GameEntry Get(DbConnection connection, DbTransaction transaction, string id, AccessMode mode)
    {
        Require(id, AccessMode.Read | mode);
        using var command = Command(
            connection,
            transaction,
            $"SELECT id,title,categories FROM {games} WHERE account=$account AND id=$id",
            (GameId, id)
        );
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : throw new KeyNotFoundException("The game does not exist.");
    }

    private void Require(string id, AccessMode mode) =>
        access.Require(ResourceUri.InAccount(PluginProject.Id, access.Account, Collection, id), mode);

    private DataReference Reference(string id) => new("games.game", id, access.Account);

    private static GameEntry ReadEntry(DbDataReader reader) =>
        new(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("title")),
            JsonSerializer.Deserialize<string[]>(reader.GetString(reader.GetOrdinal("categories")))!
        );

    private List<T> ReadJson<T>(DbConnection connection, DbTransaction transaction, string id, string sql)
    {
        using var command = Command(connection, transaction, sql, (GameId, id));
        using var reader = command.ExecuteReader();
        var records = new List<T>();
        while (reader.Read())
            records.Add(JsonSerializer.Deserialize<T>(reader.GetString(0))!);
        return records;
    }

    private DbCommand Command(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] values
    )
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.AddValue("$account", access.Account);
        foreach (var (name, value) in values)
            command.AddValue(name, value);
        return command;
    }
}
