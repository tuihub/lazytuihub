using System.Text.Json;
using LazyTuiHub.Api;
using LazyTuiHub.Cli;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Articles;

internal static class ArticleCommand
{
    public static async Task RunAsync(WebApplication app, string command, CancellationToken cancellationToken)
    {
        if (command is "help")
        {
            Console.WriteLine(
                "Articles: list [--source NAME] [--unread true], get/read --name articles/ID, search --query TEXT"
            );
            return;
        }
        var configuration = app.Configuration;
        var api = app.Services.GetRequiredService<IArticleApi>();
        switch (command)
        {
            case "list":
            case "get":
            case "read":
                await ReaderCommands.ArticlesAsync(api, configuration, command, cancellationToken);
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
}
