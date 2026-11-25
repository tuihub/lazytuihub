using System.Text.Json;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Articles.V1;

namespace LazyTuiHub.Client;

public partial class ArticleHttpClient : IArticleApi
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
        settings.Converters.Add(new ProtobufJsonConverter());

    public Task<ListArticlesResponse> ListArticlesAsync(
        ListArticlesRequest request,
        CancellationToken cancellationToken = default
    ) => ListArticlesAsync(request.PageSize, request.PageToken, request.Filter, cancellationToken);

    public Task<Article> GetArticleAsync(GetArticleRequest request, CancellationToken cancellationToken = default) =>
        GetArticleAsync(ArticleId(request.Name), cancellationToken);

    public Task<Article> MarkArticleReadAsync(
        MarkArticleReadRequest request,
        CancellationToken cancellationToken = default
    ) => MarkArticleReadAsync(ArticleId(request.Name), request, cancellationToken);

    private static string ArticleId(string name) =>
        name.Split('/') is ["articles", { Length: > 0 } id]
            ? id
            : throw new ArgumentException("Expected articles/{article}.", nameof(name));
}
