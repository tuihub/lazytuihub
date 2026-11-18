using System.Text.Json;
using Google.Protobuf;
using LazyTuiHub.Api;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Articles;

internal static class ArticleCommand
{
    public static async Task RunAsync(WebApplication app, string command, CancellationToken cancellationToken)
    {
        if (command is "help")
        {
            Console.WriteLine(
                "Articles: list [--source NAME] [--unread true], read --name articles/ID, search --query TEXT"
            );
            return;
        }
        var configuration = app.Configuration;
        var api = app.Services.GetRequiredService<IArticleApi>();
        switch (command)
        {
            case "list":
                Print(
                    await api.ListArticlesAsync(
                        new()
                        {
                            Filter = Api.Articles.ArticleFilters.ForSource(
                                configuration["source"],
                                configuration.GetValue<bool>("unread") ? "read = false" : string.Empty
                            ),
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
                        app.Services.GetRequiredService<ArticleStore>().Search(Required(configuration, "query")),
                        PipelineStore.JsonOptions
                    )
                );
                break;
            default:
                throw new ArgumentException("Choose list, read, or search.", nameof(command));
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Provide --{key}.", nameof(configuration));

    private static void Print(IMessage message) => Console.WriteLine(JsonFormatter.Default.Format(message));
}
