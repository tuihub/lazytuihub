using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    private const int ArticleIdHexLength = 32;
    private readonly IConnectionSource database;
    private readonly BusinessStorage submissions;
    private readonly ISqliteSearch search;
    private readonly DaoTable searchTable;
    private readonly string feeds;
    private readonly string articles;

    public RssStore(DaoScope daos, BusinessStorage submissions)
    {
        database = daos.Get<IConnectionSource>("database");
        this.submissions = submissions;
        search = daos.Get<ISqliteSearch>("search");
        searchTable = daos.Table("article_search");
        feeds = DaoNames.Table(daos.Table("feeds"));
        articles = DaoNames.Table(daos.Table("articles"));
        search.CreateTable(searchTable);
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {feeds} (
                id TEXT PRIMARY KEY NOT NULL,
                url TEXT UNIQUE NOT NULL,
                display_name TEXT NOT NULL,
                request_id TEXT UNIQUE
            );
            CREATE TABLE IF NOT EXISTS {articles} (
                sequence INTEGER PRIMARY KEY,
                id TEXT UNIQUE NOT NULL,
                feed_id TEXT NOT NULL REFERENCES {feeds}(id),
                entry_id TEXT NOT NULL,
                title TEXT NOT NULL,
                url TEXT NOT NULL,
                content TEXT NOT NULL,
                published_at TEXT,
                is_read INTEGER NOT NULL DEFAULT 0,
                UNIQUE(feed_id, entry_id)
            );
            CREATE INDEX IF NOT EXISTS {DaoNames.Index(
                daos.Table("articles"),
                "by_feed"
            )} ON {articles}(feed_id, sequence);
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

    public ArticleRecord Save(ArticleDraft article) => submissions.Write(database, (connection, transaction) =>
        {
            var id = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes($"{article.FeedId}\0{article.EntryId}"))
            )[..ArticleIdHexLength];
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO {articles}(id, feed_id, entry_id, title, url, content, published_at)
                VALUES ($id, $feed, $entry, $title, $url, $content, $published)
                ON CONFLICT(feed_id, entry_id) DO UPDATE SET
                    title = excluded.title, url = excluded.url, content = excluded.content,
                    published_at = excluded.published_at
                RETURNING sequence, is_read
                """;
            command.AddValue("$id", id);
            command.AddValue(FeedParameter, article.FeedId);
            command.AddValue("$entry", article.EntryId);
            command.AddValue("$title", article.Title);
            command.AddValue("$url", article.Url);
            command.AddValue("$content", article.Content);
            command.AddValue(
                "$published",
                (object?)article.PublishedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value
            );
            ArticleRecord saved;
            using (var reader = command.ExecuteReader())
            {
                reader.Read();
                saved = new(reader.GetInt64(0), id, article, reader.GetBoolean(1));
            }
            search.Index(searchTable, saved.Id, article.Title + "\n" + article.Content, transaction);
            return new BusinessChange<ArticleRecord>(
                saved,
                [saved.Reference],
                [new(new("rss.feed", article.FeedId), saved.Reference, "contains")]
            );
        });

    public IEnumerable<ArticleRecord> ReadArticles(ArticleQuery query)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT sequence, id, feed_id, entry_id, title, url, content, published_at, is_read
            FROM {articles} WHERE ($feed IS NULL OR feed_id = $feed)
                AND ($read IS NULL OR is_read = $read) AND sequence < $before
            ORDER BY sequence DESC LIMIT $count
            """;
        command.AddValue(FeedParameter, (object?)query.FeedId ?? DBNull.Value);
        command.AddValue("$read", query.Read is { } read ? read : DBNull.Value);
        command.AddValue("$before", query.BeforeSequence);
        command.AddValue("$count", query.Count);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return ReadArticle(reader);
    }

    public ArticleRecord GetArticle(ArticleSelection selection, bool markRead = false)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        if (markRead)
            command.CommandText = $"""
                UPDATE {articles} SET is_read = 1 WHERE feed_id = $feed AND id = $id
                RETURNING sequence, id, feed_id, entry_id, title, url, content, published_at, is_read
                """;
        else
            command.CommandText = $"""
                SELECT sequence, id, feed_id, entry_id, title, url, content, published_at, is_read
                FROM {articles} WHERE feed_id = $feed AND id = $id
                """;
        command.AddValue(FeedParameter, selection.FeedId);
        command.AddValue("$id", selection.ArticleId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadArticle(reader) : throw new KeyNotFoundException("The article does not exist.");
    }

    public IReadOnlyList<string> Search(string query) => search.Search(searchTable, query);

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

    private static ArticleRecord ReadArticle(DbDataReader reader) =>
        new(
            reader.GetInt64(reader.GetOrdinal("sequence")),
            reader.GetString(reader.GetOrdinal("id")),
            new(
                reader.GetString(reader.GetOrdinal("feed_id")),
                reader.GetString(reader.GetOrdinal("entry_id")),
                reader.GetString(reader.GetOrdinal("title")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("content")),
                reader.IsDBNull(reader.GetOrdinal("published_at"))
                    ? null
                    : DateTimeOffset.Parse(
                        reader.GetString(reader.GetOrdinal("published_at")),
                        CultureInfo.InvariantCulture
                    )
            ),
            reader.GetBoolean(reader.GetOrdinal("is_read"))
        );
}
