using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games;

internal static class GameCommand
{
    internal static string Required(IConfiguration config, string name) =>
        !string.IsNullOrWhiteSpace(config[name])
            ? config[name]!
            : throw new ArgumentException($"Provide --{name}.", nameof(name));

    internal static void Print(object value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, PipelineStore.JsonOptions));

    public static async Task RunAsync(WebApplication app, string action, CancellationToken token)
    {
        var config = app.Configuration;
        string Value(string key) => Required(config, key);
        var library = app.Services.GetRequiredService<GameLibrary>();
        switch (action)
        {
            case "list":
                Print(library.List());
                break;
            case "add":
                Print(library.Add(Value("title")));
                break;
            case "show":
                Print(library.Show(Value("game")));
                break;
            case "edit":
                Print(library.Edit(Value("game"), config["title"], config["add-category"], config["remove-category"]));
                break;
            case "log-session":
                library.LogSession(
                    Value("game"),
                    new(
                        Value("device"),
                        DateTimeOffset.Parse(Value("start"), CultureInfo.InvariantCulture),
                        TimeSpan.Parse(Value("duration"), CultureInfo.InvariantCulture)
                    )
                );
                break;
            case "matches":
                await using (var stream = File.OpenRead(Value("dataset")))
                    Print(await GameIdDataset.FindAsync(stream, Value("provider"), Value("source-id"), token));
                break;
            case "help":
                Console.WriteLine(
                    "Games: list, add --title TITLE, show/edit --game ID [--title TITLE] [--add-category NAME] [--remove-category NAME], log-session --game ID --device NAME --start ISO_TIME --duration HH:MM:SS, matches --dataset FILE --provider PLUGIN --source-id ID."
                );
                break;
            default:
                throw new ArgumentException($"Unknown games action '{action}'.", nameof(action));
        }
    }
}
