using System.Globalization;
using Avalonia.Data.Converters;
using LazyTuiHub.Api.Articles.V1;

namespace LazyTuiHub.Articles.Desktop;

public static class ArticleDisplay
{
    public static FuncValueConverter<Article?, string> Title { get; } =
        new(article => string.IsNullOrWhiteSpace(article?.Title) ? "Untitled article" : article.Title);

    public static FuncValueConverter<Article?, string> Source { get; } =
        new(article =>
            Uri.TryCreate(article?.Uri, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri.Host
                : article?.Source ?? string.Empty
        );

    public static FuncValueConverter<Article?, string> Date { get; } =
        new(article =>
            article?.PublishTime?.ToDateTime().ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture)
            ?? string.Empty
        );
}
