using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using LazyTuiHub.Api.Articles;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Articles;

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
public sealed class ArticleStore
{
    private const string SourceParameter = "$source";
    private readonly IConnectionSource database;
    private readonly BusinessStorage submissions;
    private readonly ISqliteSearch search;
    private readonly DaoTable searchTable;
    private readonly string articles;

    public ArticleStore(DaoScope daos, BusinessStorage submissions)
    {
        database = daos.Get<IConnectionSource>("database");
        this.submissions = submissions;
        search = daos.Get<ISqliteSearch>("search");
        searchTable = daos.Table("article_search");
        articles = DaoNames.Table(daos.Table("articles"));
        search.CreateTable(searchTable);
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {articles} (
                sequence INTEGER PRIMARY KEY,
                id TEXT UNIQUE NOT NULL,
                source TEXT NOT NULL,
                entry_id TEXT NOT NULL,
                title TEXT NOT NULL,
                url TEXT NOT NULL,
                content TEXT NOT NULL,
                published_at TEXT,
                is_read INTEGER NOT NULL DEFAULT 0,
                UNIQUE(source, entry_id)
            );
            CREATE INDEX IF NOT EXISTS {DaoNames.Index(
                daos.Table("articles"),
                "by_source"
            )} ON {articles}(source, sequence);
            """;
        command.ExecuteNonQuery();
    }

    public ArticleRecord Save(ArticleDraft article, DataReference? origin) =>
        submissions.Write(database, (connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO {articles}(id, source, entry_id, title, url, content, published_at)
                VALUES (lower(hex(randomblob(16))), $source, $entry, $title, $url, $content, $published)
                ON CONFLICT(source, entry_id) DO UPDATE SET
                    title = excluded.title, url = excluded.url, content = excluded.content,
                    published_at = excluded.published_at
                RETURNING sequence, id, is_read
                """;
            command.AddValue(SourceParameter, article.Source);
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
                saved = new(
                    reader.GetInt64(reader.GetOrdinal("sequence")),
                    reader.GetString(reader.GetOrdinal("id")),
                    article,
                    reader.GetBoolean(reader.GetOrdinal("is_read"))
                );
            }
            search.Index(searchTable, saved.Id, article.Title + "\n" + article.Content, transaction);
            return new BusinessChange<ArticleRecord>(
                saved,
                [saved.Reference],
                origin is null ? [] : [new(origin, saved.Reference, "contains")]
            );
        });

    public IEnumerable<ArticleRecord> ReadArticles(ArticleQuery query)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT sequence, id, source, entry_id, title, url, content, published_at, is_read
            FROM {articles} WHERE ($source IS NULL OR source = $source)
                AND ($read IS NULL OR is_read = $read) AND sequence < $before
            ORDER BY sequence DESC LIMIT $count
            """;
        command.AddValue(SourceParameter, (object?)query.Source ?? DBNull.Value);
        command.AddValue("$read", query.Read is { } read ? read : DBNull.Value);
        command.AddValue("$before", query.BeforeSequence);
        command.AddValue("$count", query.Count);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return ReadArticle(reader);
    }

    public ArticleRecord GetArticle(string id, bool markRead = false)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        if (markRead)
            command.CommandText = $"""
                UPDATE {articles} SET is_read = 1 WHERE id = $id
                RETURNING sequence, id, source, entry_id, title, url, content, published_at, is_read
                """;
        else
            command.CommandText = $"""
                SELECT sequence, id, source, entry_id, title, url, content, published_at, is_read
                FROM {articles} WHERE id = $id
                """;
        command.AddValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadArticle(reader) : throw new KeyNotFoundException("The article does not exist.");
    }

    public IReadOnlyList<string> Search(string query) => search.Search(searchTable, query);

    private static ArticleRecord ReadArticle(DbDataReader reader) =>
        new(
            reader.GetInt64(reader.GetOrdinal("sequence")),
            reader.GetString(reader.GetOrdinal("id")),
            new(
                reader.GetString(reader.GetOrdinal("source")),
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
