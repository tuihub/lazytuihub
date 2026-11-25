using System.Net;
using System.Text.Json;
using LazyTuiHub.Plugin.TuiHub.Games;
using Microsoft.Extensions.Configuration;

namespace LazyTuiHub.Plugin.TuiHub.Steam;

internal sealed class SteamCatalog(SteamStoreClient client, IConfiguration config) : IGameCatalog
{
    private const string Provider = "steam";
    private const string Store = "https://store.steampowered.com";
    private readonly string? snapshot = config["Games:steam:Snapshot"];
    private readonly string locale =
        $"l={Uri.EscapeDataString(config["Games:steam:Language"] ?? "english")}&cc={Uri.EscapeDataString(config["Games:steam:Country"] ?? "US")}";

    public async Task<GameMetadata[]> SearchAsync(string query, CancellationToken token)
    {
        if (snapshot is not null)
            return await GameSnapshot.SearchAsync(snapshot, Provider, query, token);
        var response = await client.ReadAsync(
            new Uri($"{Store}/api/storesearch/?term={Uri.EscapeDataString(query)}&{locale}"),
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

    public async Task<GameMetadata> LookupAsync(string id, CancellationToken token)
    {
        if (snapshot is not null)
            return await GameSnapshot.LookupAsync(snapshot, Provider, id, token);
        var response = await client.ReadAsync(
            new Uri($"{Store}/api/appdetails?appids={NumericId(id)}&{locale}"),
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

    private static string? Text(JsonElement row, string field) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(field, out var value) ? value.GetString() : null;

    private static string NumericId(string id) =>
        id.Length > 0 && id[0] != '0' && id.All(char.IsAsciiDigit)
            ? id
            : throw new ArgumentException("Provide a positive decimal source ID.", nameof(id));
}
