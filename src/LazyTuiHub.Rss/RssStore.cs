using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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
    private readonly string feeds;

    public RssStore(DaoScope daos, BusinessStorage submissions)
    {
        database = daos.Get<IConnectionSource>("database");
        this.submissions = submissions;
        feeds = DaoNames.Table(daos.Table("feeds"));
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {feeds} (
                id TEXT PRIMARY KEY NOT NULL,
                url TEXT UNIQUE NOT NULL,
                display_name TEXT NOT NULL,
                request_id TEXT UNIQUE
            );
            """;
        command.ExecuteNonQuery();
    }

    public FeedRecord Subscribe(SubscribeFeed request) => submissions.Write(database, (connection, transaction) =>
        {
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
                displayName
            );
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO {feeds}(id, url, display_name, request_id) VALUES ($id, $url, $title, $request)
                ON CONFLICT DO NOTHING
                """;
            command.AddValue("$id", feed.Id);
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
            WHERE ($feed IS NULL OR id = $feed) AND id > $after ORDER BY id LIMIT $count
            """;
        command.AddValue(FeedParameter, (object?)selection.FeedId ?? DBNull.Value);
        command.AddValue("$after", selection.AfterId);
        command.AddValue("$count", selection.Count);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("display_name"))
            );
    }

    private FeedRecord? ReadRequest(DbConnection connection, DbTransaction transaction, string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return null;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT id, url, display_name FROM {feeds} WHERE request_id = $request";
        command.AddValue("$request", requestId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("display_name"))
            )
            : null;
    }
}
