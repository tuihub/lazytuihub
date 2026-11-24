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
        var connections = app.Services.GetRequiredService<GameConnections>();
        var curation = app.Services.GetRequiredService<GameCuration>();
        switch (action)
        {
            case "list":
                Print(library.List());
                break;
            case "shelf":
                Print(library.Shelf());
                break;
            case "connections":
                Print(library.Origins());
                break;
            case "bind":
                var platform = Value("provider");
                var needsToken = connections
                    .Providers.Single(p => p.Id.Equals(platform, StringComparison.Ordinal))
                    .UsesToken;
                var secret = needsToken ? await ReadTokenAsync(token) : string.Empty;
                Print(
                    await connections.BindAsync(
                        platform,
                        secret,
                        uri => Console.Error.WriteLine(uri.AbsoluteUri),
                        token
                    )
                );
                break;
            case "sync":
                Print(await connections.SyncAsync(Value("connection"), token));
                break;
            case "disconnect":
                library.Disconnect(Value("connection"));
                break;
            case "scan":
                Print(await connections.ScanAsync(Value("scanner"), Value("root"), token));
                break;
            case "fill":
                Print(await curation.FillAsync(Value("game"), token));
                break;
            case "candidates":
                Print(await curation.CandidatesAsync(Value("game"), token));
                break;
            case "unlink":
                library.Unlink(Value("game"), Value("provider"), Value("source-id"));
                break;
            case "set-field":
                library.SetField(Value("game"), Value("field"), config["value"]);
                break;
            case "assign":
                library.Assign(Value("connection"), Value("evidence"), Value("game"));
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
                    "Games: shelf, connections, bind --provider PLUGIN (supply personal tokens on stdin), sync/disconnect --connection ID, scan --scanner steam|folder --root PATH, candidates/fill --game ID, unlink --game ID --provider PLUGIN --source-id ID, set-field --game ID --field NAME [--value TEXT], assign --connection ID --evidence KEY --game ID, list, add --title TITLE, show/edit --game ID [--title TITLE] [--add-category NAME] [--remove-category NAME], log-session --game ID --device NAME --start ISO_TIME --duration HH:MM:SS, matches --dataset FILE --provider PLUGIN --source-id ID."
                );
                break;
            default:
                throw new ArgumentException($"Unknown games action '{action}'.", nameof(action));
        }
    }

    private static async Task<string> ReadTokenAsync(CancellationToken token)
    {
        if (Console.IsInputRedirected)
            return await Console.In.ReadLineAsync(token)
                ?? throw new EndOfStreamException("Supply the platform token on stdin.");
        await Console.Error.WriteAsync("Platform access token: ");
        var secret = new System.Text.StringBuilder();
        while (true)
        {
            var input = Console.ReadKey(intercept: true);
            if (input.Key == ConsoleKey.Enter)
                break;
            if (input.Key == ConsoleKey.Backspace && secret.Length > 0)
                secret.Length--;
            else if (!char.IsControl(input.KeyChar))
                secret.Append(input.KeyChar);
        }
        await Console.Error.WriteLineAsync();
        return secret.ToString();
    }
}
