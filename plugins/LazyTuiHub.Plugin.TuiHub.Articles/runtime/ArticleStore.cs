using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
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
    private const string CollectionName = "articles";
    private const string SourceParameter = "$source";
    private readonly IConnectionSource database;
    private readonly BusinessStorage submissions;
    private readonly ResourceAccess access;
    private string Collection => ResourceUri.InAccount(ArticlePlugin.Id, access.Account, CollectionName);
    private readonly ISqliteSearch search;
    private readonly DaoTable searchTable;
    private readonly string articles;

    public ArticleStore(DaoScope daos, BusinessStorage submissions, ResourceAccess access)
    {
        database = daos.Get<IConnectionSource>("database");
        this.submissions = submissions;
        this.access = access;
        search = daos.Get<ISqliteSearch>("search");
        searchTable = daos.Table("article_search");
        articles = DaoNames.Table(daos.Table(CollectionName));
        search.CreateTable(searchTable);
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {articles} (
                sequence INTEGER PRIMARY KEY,
                id TEXT UNIQUE NOT NULL,
                account_id TEXT NOT NULL,
                source TEXT NOT NULL,
                entry_id TEXT NOT NULL,
                title TEXT NOT NULL,
                url TEXT NOT NULL,
                content TEXT NOT NULL,
                published_at TEXT,
                is_read INTEGER NOT NULL DEFAULT 0,
                UNIQUE(account_id, source, entry_id)
            );
            CREATE INDEX IF NOT EXISTS {DaoNames.Index(
                daos.Table(CollectionName),
                "by_source"
            )} ON {articles}(account_id, source, sequence);
            """;
        command.ExecuteNonQuery();
    }

    public ArticleRecord Save(ArticleDraft article, DataReference? origin) =>
        submissions.Write(database, (connection, transaction) =>
        {
            access.Require(Collection, AccessMode.Write);
            var account = access.Account;
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO {articles}(id, account_id, source, entry_id, title, url, content, published_at)
                VALUES (lower(hex(randomblob(16))), $account, $source, $entry, $title, $url, $content, $published)
                ON CONFLICT(account_id, source, entry_id) DO UPDATE SET
                    title = excluded.title, url = excluded.url, content = excluded.content,
                    published_at = excluded.published_at
                RETURNING sequence, id, is_read
                """;
            command.AddValue("$account", account);
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
                    reader.GetBoolean(reader.GetOrdinal("is_read")),
                    account
                );
            }
            search.Index(searchTable, saved.Id, article.Title + "\n" + article.Content, transaction, account);
            return new BusinessChange<ArticleRecord>(
                saved,
                origin is null ? [saved.Reference] : [origin, saved.Reference],
                origin is null ? [] : [new(origin, saved.Reference, "contains")]
            );
        });

    public IEnumerable<ArticleRecord> ReadArticles(ArticleQuery query)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT sequence, id, account_id, source, entry_id, title, url, content, published_at, is_read
            FROM {articles} WHERE account_id = $account AND ($source IS NULL OR source = $source)
                AND ($allowed IS NULL OR id IN (SELECT value FROM json_each($allowed)))
                AND ($read IS NULL OR is_read = $read) AND sequence < $before
            ORDER BY sequence DESC LIMIT $count
            """;
        command.AddValue("$account", access.Account);
        command.AddValue("$allowed", ScopeParameter());
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
        var uri = ResourceUri.InAccount(ArticlePlugin.Id, access.Account, CollectionName, id);
        access.Require(uri, markRead ? AccessMode.Read | AccessMode.Write : AccessMode.Read);
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        if (markRead)
            command.CommandText = $"""
                UPDATE {articles} SET is_read = 1 WHERE account_id = $account AND id = $id
                RETURNING sequence, id, account_id, source, entry_id, title, url, content, published_at, is_read
                """;
        else
            command.CommandText = $"""
                SELECT sequence, id, account_id, source, entry_id, title, url, content, published_at, is_read
                FROM {articles} WHERE account_id = $account AND id = $id
                """;
        command.AddValue("$account", access.Account);
        command.AddValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadArticle(reader) : throw new KeyNotFoundException("The article does not exist.");
    }

    public IReadOnlyList<string> Search(string query) =>
        search.Search(searchTable, query, access.Account, access.ReadableChildren(Collection));

    private object ScopeParameter() =>
        access.ReadableChildren(Collection) is { } ids ? JsonSerializer.Serialize(ids) : DBNull.Value;

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
            reader.GetBoolean(reader.GetOrdinal("is_read")),
            reader.GetString(reader.GetOrdinal("account_id"))
        );
}
