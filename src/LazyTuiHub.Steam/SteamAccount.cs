using System.Globalization;
using LazyTuiHub.Games;
using Microsoft.Extensions.Configuration;

namespace LazyTuiHub.Steam;

internal sealed class SteamAccount(IConfiguration config) : GameAccountProvider
{
    public override string Id => "steam";
    public override Uri SignInPage { get; } = new("https://steamcommunity.com/openid/login");
    public override bool UsesToken => false;

    public override async Task<PlatformIdentity> IdentifyAsync(
        HttpClient client,
        string secret,
        Action<Uri> showBrowser,
        CancellationToken token
    )
    {
        var id = await SteamSignIn.AuthenticateAsync(client, showBrowser, token);
        return new(id, id);
    }

    public override async Task<IReadOnlyList<PlatformGame>> LibraryAsync(
        HttpClient client,
        string id,
        string secret,
        CancellationToken token
    )
    {
        var key = config["Games:steam:WebApiKey"]
            ?? throw new InvalidOperationException(
                "Configure Games:steam:WebApiKey on the host to sync Steam libraries."
            );
        var url = new Uri(
            $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?steamid={Uri.EscapeDataString(id)}&include_appinfo=1&include_played_free_games=1"
        );
        var json = await ReadAsync(client, url, "x-webapi-key", key, null, token);
        var response = json.GetProperty("response");
        if (!response.TryGetProperty("game_count", out var count))
            throw new InvalidOperationException(
                "Steam did not disclose this library. Check Game details privacy in Steam."
            );
        if (count.GetInt32() == 0)
            return [];
        var rows = response.GetProperty("games");
        if (rows.GetArrayLength() != count.GetInt32())
            throw new InvalidDataException("Steam returned an incomplete library.");
        return rows.EnumerateArray()
            .Select(row => new PlatformGame(
                row.GetProperty("appid").GetInt64().ToString(CultureInfo.InvariantCulture),
                row.GetProperty("name").GetString()!,
                "In Steam library"
            ))
            .ToArray();
    }
}
