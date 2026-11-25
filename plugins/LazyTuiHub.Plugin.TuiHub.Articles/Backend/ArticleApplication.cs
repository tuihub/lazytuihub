using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Articles.V1;
using LazyTuiHub.Api.Hosting;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Sdk;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal sealed class ArticleApplication(
    IWorkflowExecutor workflows,
    IDataProtectionProvider protection,
    ILogger<ArticleApplication> logger,
    ResourceAccess access
) : IArticleApi
{
    private readonly PageTokens tokens = new(protection);

    public Task<ListArticlesResponse> ListArticlesAsync(
        ListArticlesRequest request,
        CancellationToken cancellationToken = default
    ) =>
        RunAsync(async () =>
        {
            var size = PageTokens.PageSize(request.PageSize);
            var (source, read) = ArticleFilter.Parse(request.Filter);
            var scope = $"{access.Account}:articles:{request.Filter}";
            var before = long.Parse(
                tokens.Decode(scope, request.PageToken, long.MaxValue.ToString(CultureInfo.InvariantCulture)),
                CultureInfo.InvariantCulture
            );
            var articles = await workflows.CollectAsync<ArticleRecord>(
                "articles.list",
                new ArticleQuery(source, read, before, (long)size + 1),
                cancellationToken
            );
            var response = new ListArticlesResponse();
            response.Articles.AddRange(articles.Take(size).Select(ToArticle));
            if (articles.Count > size)
                response.NextPageToken = tokens.Encode(
                    scope,
                    articles[size - 1].Sequence.ToString(CultureInfo.InvariantCulture)
                );
            return response;
        });

    public Task<Article> GetArticleAsync(GetArticleRequest request, CancellationToken cancellationToken = default) =>
        ReadArticleAsync("articles.get", request.Name, cancellationToken);

    public Task<Article> MarkArticleReadAsync(
        MarkArticleReadRequest request,
        CancellationToken cancellationToken = default
    ) => ReadArticleAsync("articles.mark-read", request.Name, cancellationToken);

    private Task<Article> ReadArticleAsync(string pipeline, string name, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var article = ArticleServicePaths.ArticleId(name);
            var articles = await workflows.CollectAsync<ArticleRecord>(pipeline, article, cancellationToken);
            return ToArticle(articles.Single());
        });

    private Task<T> RunAsync<T>(Func<Task<T>> run) => ApiErrors.RunAsync(run, logger);

    private static Article ToArticle(ArticleRecord record) =>
        new()
        {
            Name = $"articles/{record.Id}",
            Source = record.Article.Source,
            Title = record.Article.Title,
            Uri = record.Article.Url,
            Content = record.Article.Content,
            Read = record.IsRead,
            PublishTime = record.Article.PublishedAt is { } published ? Timestamp.FromDateTimeOffset(published) : null,
        };
}
