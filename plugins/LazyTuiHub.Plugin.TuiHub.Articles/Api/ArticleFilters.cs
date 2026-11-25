using System.Text.Json;

namespace LazyTuiHub.Plugin.TuiHub.Articles.Api;

public static class ArticleFilters
{
    public static string ForSource(string? source, string filter = "")
    {
        if (string.IsNullOrEmpty(source))
            return filter;
        var predicate = $"source = \"{JsonEncodedText.Encode(source)}\"";
        return string.IsNullOrEmpty(filter) ? predicate : $"{predicate} AND {filter}";
    }
}
