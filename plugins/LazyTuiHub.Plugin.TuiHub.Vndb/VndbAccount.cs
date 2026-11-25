using LazyTuiHub.Plugin.TuiHub.Games;

namespace LazyTuiHub.Plugin.TuiHub.Vndb;

internal sealed class VndbAccount : GameAccountProvider
{
    private static readonly Uri Api = new("https://api.vndb.org/kana/");
    public override string Id => "vndb";
    public override Uri SignInPage { get; } = new("https://vndb.org/u/tokens");

    public override Task<PlatformIdentity> IdentifyAsync(
        HttpClient client,
        string secret,
        Action<Uri> showBrowser,
        CancellationToken token
    ) => IdentityAsync(client, secret, token);

    private static async Task<PlatformIdentity> IdentityAsync(HttpClient client, string secret, CancellationToken token)
    {
        var me = await ReadAsync(client, new Uri(Api, "authinfo"), "Token", secret, null, token);
        if (!me.GetProperty("permissions").EnumerateArray().Any(item => item.ValueEquals("listread")))
            throw new InvalidOperationException("Enable listread for this VNDB token to include private entries.");
        return new(me.GetProperty("id").GetString()!, me.GetProperty("username").GetString()!);
    }

    public override async Task<IReadOnlyList<PlatformGame>> LibraryAsync(
        HttpClient client,
        string id,
        string secret,
        CancellationToken token
    )
    {
        var identity = await IdentityAsync(client, secret, token);
        if (!identity.Id.Equals(id, StringComparison.Ordinal))
            throw new InvalidOperationException("Reconnect the VNDB account.");
        // Kana caps page-number pagination. Walk IDs so large personal libraries remain readable.
        const int pageSize = 100;
        var games = new List<PlatformGame>();
        string? after = null;
        while (true)
        {
            var page = await ReadAsync(
                client,
                new Uri(Api, "ulist"),
                "Token",
                secret,
                new
                {
                    user = id,
                    fields = "id,vn.title,labels.label",
                    sort = "id",
                    results = pageSize,
                    filters = after is null ? Array.Empty<string>() : new[] { "id", ">", after },
                },
                token
            );
            var rows = page.GetProperty("results");
            foreach (var row in rows.EnumerateArray())
            {
                var labels = string.Join(
                    ", ",
                    row.GetProperty("labels").EnumerateArray().Select(label => label.GetProperty("label").GetString())
                );
                games.Add(
                    new(
                        row.GetProperty("id").GetString()!,
                        row.GetProperty("vn").GetProperty("title").GetString()!,
                        labels.Length == 0 ? "Collected" : labels
                    )
                );
            }
            if (!page.GetProperty("more").GetBoolean())
                return games;
            if (rows.GetArrayLength() == 0 || string.Equals(after, games[^1].Id, StringComparison.Ordinal))
                throw new InvalidDataException("VNDB did not advance the collection cursor.");
            after = games[^1].Id;
        }
    }
}
