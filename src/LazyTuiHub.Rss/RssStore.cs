using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All interpolated table/index identifiers come from hexadecimal DaoNames; record values use parameters."
)]
[SuppressMessage(
    "Major Code Smell",
    "S2077:Formatting SQL queries is security-sensitive",
    Justification = "Interpolated identifiers are generated from fixed prefixes and hexadecimal encoding."
)]
public sealed class RssStore
{
    private const string FeedParameter = "$feed";
    private readonly IConnectionSource database;
    private readonly BusinessStorage submissions;
    private readonly ResourceAccess access;
    private string Collection => ResourceUri.InAccount("rss", access.Account, "feeds");
    private readonly string feeds;

    public RssStore(DaoScope daos, BusinessStorage submissions, ResourceAccess access)
    {
        database = daos.Get<IConnectionSource>("database");
        this.submissions = submissions;
        this.access = access;
        feeds = DaoNames.Table(daos.Table("feeds"));
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {feeds} (
                id TEXT NOT NULL,
                account_id TEXT NOT NULL,
                url TEXT NOT NULL,
                display_name TEXT NOT NULL,
                request_id TEXT,
                PRIMARY KEY(account_id, id),
                UNIQUE(account_id, url),
                UNIQUE(account_id, request_id)
            );
            """;
        command.ExecuteNonQuery();
    }

    public FeedRecord Subscribe(SubscribeFeed request) => submissions.Write(database, (connection, transaction) =>
        {
            access.Require(Collection, AccessMode.Read | AccessMode.Write);
            var account = access.Account;
            var displayName = string.IsNullOrEmpty(request.DisplayName) ? request.Url.Host : request.DisplayName;
            var existing = ReadRequest(connection, transaction, request.RequestId);
            if (existing is not null)
            {
                if (
                    !existing.Url.Equals(request.Url.AbsoluteUri, StringComparison.Ordinal)
                    || !existing.DisplayName.Equals(displayName, StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(request.Id) && !existing.Id.Equals(request.Id, StringComparison.Ordinal))
                )
                    throw new RssConflictException(
                        "REQUEST_ID_REUSED",
                        "The request ID was already used for a different subscription."
                    );
                return new BusinessChange<FeedRecord>(existing, [existing.Reference], []);
            }
            var feed = new FeedRecord(
                string.IsNullOrEmpty(request.Id) ? Guid.NewGuid().ToString("D") : request.Id,
                request.Url.AbsoluteUri,
                displayName,
                account
            );
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO {feeds}(id, account_id, url, display_name, request_id) VALUES ($id, $account, $url, $title, $request)
                ON CONFLICT DO NOTHING
                """;
            command.AddValue("$id", feed.Id);
            command.AddValue("$account", account);
            command.AddValue("$url", feed.Url);
            command.AddValue("$title", feed.DisplayName);
            command.AddValue("$request", string.IsNullOrEmpty(request.RequestId) ? DBNull.Value : request.RequestId);
            if (command.ExecuteNonQuery() == 0)
                throw new RssConflictException("FEED_EXISTS", "A subscription with that ID or URL already exists.");
            return new BusinessChange<FeedRecord>(feed, [feed.Reference], []);
        });

    public IEnumerable<FeedRecord> ReadFeeds(FeedSelection selection)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, url, display_name FROM {feeds}
            WHERE account_id = $account AND ($feed IS NULL OR id = $feed) AND id > $after
                AND ($allowed IS NULL OR id IN (SELECT value FROM json_each($allowed)))
            ORDER BY id LIMIT $count
            """;
        command.AddValue("$account", access.Account);
        command.AddValue(
            "$allowed",
            access.ReadableChildren(Collection) is { } ids ? JsonSerializer.Serialize(ids) : DBNull.Value
        );
        command.AddValue(FeedParameter, (object?)selection.FeedId ?? DBNull.Value);
        command.AddValue("$after", selection.AfterId);
        command.AddValue("$count", selection.Count);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("display_name")),
                access.Account
            );
    }

    private FeedRecord? ReadRequest(DbConnection connection, DbTransaction transaction, string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return null;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT id, url, display_name FROM {feeds} WHERE account_id = $account AND request_id = $request";
        command.AddValue("$request", requestId);
        command.AddValue("$account", access.Account);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("display_name")),
                access.Account
            )
            : null;
    }
}
