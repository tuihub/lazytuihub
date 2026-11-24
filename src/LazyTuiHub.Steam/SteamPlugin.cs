using System.Net;
using System.Text.Json;
using LazyTuiHub.Games;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Steam;

public sealed class SteamPlugin : GameProviderPlugin
{
    private const string Store = "https://store.steampowered.com";
    private const string Provider = "steam";

    private string locale = string.Empty;

    public override void Configure(PluginRegistration registration)
    {
        var language = registration.Configuration["Games:steam:Language"] ?? "english";
        var country = registration.Configuration["Games:steam:Country"] ?? "US";
        locale = $"l={Uri.EscapeDataString(language)}&cc={Uri.EscapeDataString(country)}";
        base.Configure(registration);
    }

    protected override async Task<GameMetadata[]> SearchAsync(HttpClient client, string query, CancellationToken token)
    {
        var response = await RequestAsync(
            client,
            new Uri($"{Store}/api/storesearch/?term={Uri.EscapeDataString(query)}&{locale}"),
            null,
            token
        );
        return response
            .GetProperty("items")
            .EnumerateArray()
            .Where(item => item.GetProperty("type").ValueEquals("app"))
            .Select(item =>
            {
                var id = item.GetProperty("id").GetRawText();
                return new GameMetadata(
                    Provider,
                    id,
                    item.GetProperty("name").GetString()!,
                    Store + "/app/" + id,
                    ImageUrl: Text(item, "tiny_image")
                );
            })
            .ToArray();
    }

    protected override async Task<GameMetadata> LookupAsync(HttpClient client, string id, CancellationToken token)
    {
        var response = await RequestAsync(
            client,
            new Uri($"{Store}/api/appdetails?appids={NumericId(id)}&{locale}"),
            null,
            token
        );
        var result = response.GetProperty(id);
        if (!result.GetProperty("success").GetBoolean())
            throw new KeyNotFoundException($"Steam app '{id}' is unavailable.");
        var game = result.GetProperty("data");
        return new(
            Provider,
            id,
            game.GetProperty("name").GetString()!,
            Store + "/app/" + id,
            WebUtility.HtmlDecode(Text(game, "short_description")),
            Text(game, "header_image"),
            game.TryGetProperty("release_date", out var date) ? Text(date, "date") : null,
            Developers: Names(game, "developers"),
            Publishers: Names(game, "publishers"),
            SourceKind: Text(game, "type")
        );
    }

    private static string[]? Names(JsonElement row, string field) =>
        row.TryGetProperty(field, out var values) ? values.Deserialize<string[]>() : null;
}
