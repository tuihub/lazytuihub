using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LazyTuiHub.Framework;
using Microsoft.Data.Sqlite;

namespace LazyTuiHub.Rss;

public sealed class RssStore
{
    private const string FeedParameter = "$feed";
    private const int ArticleIdHexLength = 32;
    private readonly SqliteDatabase database;

    public RssStore(SqliteDatabase database)
    {
        this.database = database;
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS rss_feeds (
                id TEXT PRIMARY KEY NOT NULL,
                url TEXT UNIQUE NOT NULL,
                display_name TEXT NOT NULL,
                request_id TEXT UNIQUE
            );
            CREATE TABLE IF NOT EXISTS rss_articles (
                sequence INTEGER PRIMARY KEY,
                id TEXT UNIQUE NOT NULL,
                feed_id TEXT NOT NULL REFERENCES rss_feeds(id),
                entry_id TEXT NOT NULL,
                title TEXT NOT NULL,
                url TEXT NOT NULL,
                content TEXT NOT NULL,
                published_at TEXT,
                is_read INTEGER NOT NULL DEFAULT 0,
                UNIQUE(feed_id, entry_id)
            );
            CREATE INDEX IF NOT EXISTS rss_articles_by_feed ON rss_articles(feed_id, sequence);
            """;
        command.ExecuteNonQuery();
    }

    public FeedRecord Subscribe(SubscribeFeed request)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
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
            return existing;
        }
        var feed = new FeedRecord(
            string.IsNullOrEmpty(request.Id) ? Guid.NewGuid().ToString("D") : request.Id,
            request.Url.AbsoluteUri,
            displayName
        );
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO rss_feeds(id, url, display_name, request_id) VALUES ($id, $url, $title, $request)
            ON CONFLICT DO NOTHING
            """;
        command.Parameters.AddWithValue("$id", feed.Id);
        command.Parameters.AddWithValue("$url", feed.Url);
        command.Parameters.AddWithValue("$title", feed.DisplayName);
        command.Parameters.AddWithValue(
            "$request",
            string.IsNullOrEmpty(request.RequestId) ? DBNull.Value : request.RequestId
        );
        if (command.ExecuteNonQuery() == 0)
            throw new RssConflictException("FEED_EXISTS", "A subscription with that ID or URL already exists.");
        DataCatalog.Register(connection, transaction, feed.Reference);
        transaction.Commit();
        return feed;
    }

    public IEnumerable<FeedRecord> ReadFeeds(FeedSelection selection)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, url, display_name FROM rss_feeds
            WHERE ($feed IS NULL OR id = $feed) AND id > $after ORDER BY id LIMIT $count
            """;
        command.Parameters.AddWithValue(FeedParameter, (object?)selection.FeedId ?? DBNull.Value);
        command.Parameters.AddWithValue("$after", selection.AfterId);
        command.Parameters.AddWithValue("$count", selection.Count);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("display_name"))
            );
    }

    public ArticleRecord Save(ArticleDraft article)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var id = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{article.FeedId}\0{article.EntryId}"))
        )[..ArticleIdHexLength];
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO rss_articles(id, feed_id, entry_id, title, url, content, published_at)
            VALUES ($id, $feed, $entry, $title, $url, $content, $published)
            ON CONFLICT(feed_id, entry_id) DO UPDATE SET
                title = excluded.title, url = excluded.url, content = excluded.content,
                published_at = excluded.published_at
            RETURNING sequence, is_read
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue(FeedParameter, article.FeedId);
        command.Parameters.AddWithValue("$entry", article.EntryId);
        command.Parameters.AddWithValue("$title", article.Title);
        command.Parameters.AddWithValue("$url", article.Url);
        command.Parameters.AddWithValue("$content", article.Content);
        command.Parameters.AddWithValue(
            "$published",
            (object?)article.PublishedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value
        );
        ArticleRecord saved;
        using (var reader = command.ExecuteReader())
        {
            reader.Read();
            saved = new(reader.GetInt64(0), id, article, reader.GetBoolean(1));
        }
        DataCatalog.Register(connection, transaction, saved.Reference);
        DataCatalog.Relate(connection, transaction, new(new("rss.feed", article.FeedId), saved.Reference, "contains"));
        transaction.Commit();
        return saved;
    }

    public IEnumerable<ArticleRecord> ReadArticles(ArticleQuery query)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sequence, id, feed_id, entry_id, title, url, content, published_at, is_read
            FROM rss_articles WHERE ($feed IS NULL OR feed_id = $feed)
                AND ($read IS NULL OR is_read = $read) AND sequence < $before
            ORDER BY sequence DESC LIMIT $count
            """;
        command.Parameters.AddWithValue(FeedParameter, (object?)query.FeedId ?? DBNull.Value);
        command.Parameters.AddWithValue("$read", query.Read is { } read ? read : DBNull.Value);
        command.Parameters.AddWithValue("$before", query.BeforeSequence);
        command.Parameters.AddWithValue("$count", query.Count);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return ReadArticle(reader);
    }

    public ArticleRecord GetArticle(ArticleSelection selection, bool markRead = false)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        if (markRead)
            command.CommandText = """
                UPDATE rss_articles SET is_read = 1 WHERE feed_id = $feed AND id = $id
                RETURNING sequence, id, feed_id, entry_id, title, url, content, published_at, is_read
                """;
        else
            command.CommandText = """
                SELECT sequence, id, feed_id, entry_id, title, url, content, published_at, is_read
                FROM rss_articles WHERE feed_id = $feed AND id = $id
                """;
        command.Parameters.AddWithValue(FeedParameter, selection.FeedId);
        command.Parameters.AddWithValue("$id", selection.ArticleId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadArticle(reader) : throw new KeyNotFoundException("The article does not exist.");
    }

    private static FeedRecord? ReadRequest(SqliteConnection connection, SqliteTransaction transaction, string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return null;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, url, display_name FROM rss_feeds WHERE request_id = $request";
        command.Parameters.AddWithValue("$request", requestId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("url")),
                reader.GetString(reader.GetOrdinal("display_name"))
            )
            : null;
    }

    private static ArticleRecord ReadArticle(SqliteDataReader reader) =>
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
