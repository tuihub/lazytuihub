using Google.Protobuf;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Articles;
using Microsoft.Extensions.Configuration;

namespace LazyTuiHub.Cli;

public static class ReaderCommands
{
    public static async Task ArticlesAsync(
        IArticleApi api,
        IConfiguration options,
        string action,
        CancellationToken token
    )
    {
        IMessage result = action switch
        {
            "list" => await api.ListArticlesAsync(
                new()
                {
                    Filter = ArticleFilters.ForSource(
                        options["source"],
                        bool.Parse(options["unread"] ?? "false") ? "read = false" : string.Empty
                    ),
                    PageToken = options["page-token"] ?? string.Empty,
                },
                token
            ),
            "get" => await api.GetArticleAsync(new() { Name = Required(options, "name") }, token),
            "read" => await api.MarkArticleReadAsync(new() { Name = Required(options, "name") }, token),
            _ => throw new ArgumentException("Choose list, get, or read.", nameof(action)),
        };
        Console.WriteLine(JsonFormatter.Default.Format(result));
    }

    public static async Task FeedsAsync(IRssApi api, IConfiguration options, string action, CancellationToken token)
    {
        IMessage result = action switch
        {
            "feeds" => await api.ListFeedsAsync(new() { PageToken = options["page-token"] ?? string.Empty }, token),
            "add" => await api.CreateFeedAsync(
                new()
                {
                    Feed = new()
                    {
                        Uri = Required(options, "feed-url"),
                        DisplayName = options["title"] ?? string.Empty,
                    },
                    FeedId = options["feed-id"] ?? string.Empty,
                    RequestId = options["request-id"] ?? Guid.NewGuid().ToString("D"),
                },
                token
            ),
            "get" => await api.GetFeedAsync(new() { Name = Required(options, "name") }, token),
            "refresh" => await api.RefreshFeedAsync(new() { Name = Required(options, "name") }, token),
            _ => throw new ArgumentException("Choose feeds, add, get, or refresh.", nameof(action)),
        };
        Console.WriteLine(JsonFormatter.Default.Format(result));
    }

    internal static string Required(IConfiguration options, string key) =>
        options[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Provide --{key}.", nameof(options));
}
