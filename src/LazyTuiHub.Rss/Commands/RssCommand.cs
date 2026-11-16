using System.Text.Json;
using Google.Protobuf;
using LazyTuiHub.Api;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss.Commands;

internal static class RssCommand
{
    public static async Task RunAsync(WebApplication app, string command, CancellationToken cancellationToken)
    {
        if (command is "help")
        {
            Console.WriteLine(
                "RSS: add --feed-url URI [--title TEXT], feeds, refresh --name feeds/ID, articles [--unread true], read --name feeds/ID/articles/ID, search --query TEXT, details [--include-expired true], keep-detail --key KEY"
            );
            return;
        }
        var configuration = app.Configuration;
        var api = app.Services.GetRequiredService<IRssApi>();
        switch (command)
        {
            case "add":
                Print(
                    await api.CreateFeedAsync(
                        new()
                        {
                            Feed = new()
                            {
                                Uri = Required(configuration, "feed-url"),
                                DisplayName = configuration["title"] ?? string.Empty,
                            },
                            FeedId = configuration["feed-id"] ?? string.Empty,
                            RequestId = configuration["request-id"] ?? Guid.NewGuid().ToString("D"),
                        },
                        cancellationToken
                    )
                );
                break;
            case "feeds":
                Print(
                    await api.ListFeedsAsync(
                        new() { PageToken = configuration["page-token"] ?? string.Empty },
                        cancellationToken
                    )
                );
                break;
            case "refresh":
                Print(await api.RefreshFeedAsync(new() { Name = Required(configuration, "name") }, cancellationToken));
                break;
            case "articles":
                Print(
                    await api.ListArticlesAsync(
                        new()
                        {
                            Parent = configuration["parent"] ?? "feeds/-",
                            Filter = configuration.GetValue<bool>("unread") ? "read = false" : string.Empty,
                            PageToken = configuration["page-token"] ?? string.Empty,
                        },
                        cancellationToken
                    )
                );
                break;
            case "read":
                Print(
                    await api.MarkArticleReadAsync(new() { Name = Required(configuration, "name") }, cancellationToken)
                );
                break;
            case "search":
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        app.Services.GetRequiredService<RssStore>().Search(Required(configuration, "query")),
                        PipelineStore.JsonOptions
                    )
                );
                break;
            case "details":
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        app.Services.GetRequiredService<RssDetails>()
                            .Read(configuration.GetValue<bool>("include-expired")),
                        PipelineStore.JsonOptions
                    )
                );
                break;
            case "keep-detail":
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        new
                        {
                            Kept = app.Services.GetRequiredService<RssDetails>().Keep(Required(configuration, "key")),
                        },
                        PipelineStore.JsonOptions
                    )
                );
                break;
            default:
                throw new ArgumentException(
                    "Choose add, feeds, refresh, articles, read, search, details, or keep-detail.",
                    nameof(command)
                );
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Provide --{key}.", nameof(configuration));

    private static void Print(IMessage message) => Console.WriteLine(JsonFormatter.Default.Format(message));
}
