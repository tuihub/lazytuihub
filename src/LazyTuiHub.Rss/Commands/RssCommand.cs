using System.Text.Json;
using LazyTuiHub.Api;
using LazyTuiHub.Cli;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss.Commands;

internal static class RssCommand
{
    public static async Task RunAsync(WebApplication app, string command, CancellationToken cancellationToken)
    {
        if (command is "help")
        {
            Console.WriteLine(
                "RSS: add --feed-url URI [--title TEXT], feeds, get/refresh --name feeds/ID, details [--include-expired true], keep-detail --key KEY"
            );
            return;
        }
        var configuration = app.Configuration;
        var api = app.Services.GetRequiredService<IRssApi>();
        switch (command)
        {
            case "add":
            case "feeds":
            case "get":
            case "refresh":
                await ReaderCommands.FeedsAsync(api, configuration, command, cancellationToken);
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
                throw new ArgumentException("Choose add, feeds, refresh, details, or keep-detail.", nameof(command));
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Provide --{key}.", nameof(configuration));
}
