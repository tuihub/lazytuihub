using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Plugin.TuiHub.Games;

namespace LazyTuiHub.Plugin.TuiHub.Bangumi;

internal sealed class BangumiAccount : GameAccountProvider
{
    private static readonly Uri Api = new("https://api.bgm.tv/v0/");
    public override string Id => "bangumi";
    public override Uri SignInPage { get; } = new("https://next.bgm.tv/demo/access-token");

    public override async Task<PlatformIdentity> IdentifyAsync(
        HttpClient client,
        string secret,
        Action<Uri> showBrowser,
        CancellationToken token
    )
    {
        var me = await ReadAsync(client, new Uri(Api, "me"), "Bearer", secret, null, token);
        return new(
            me.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture),
            me.GetProperty("nickname").GetString() ?? me.GetProperty("username").GetString()!
        );
    }

    public override async Task<IReadOnlyList<PlatformGame>> LibraryAsync(
        HttpClient client,
        string id,
        string secret,
        CancellationToken token
    )
    {
        var me = await ReadAsync(client, new Uri(Api, "me"), "Bearer", secret, null, token);
        if (!me.GetProperty("id").GetRawText().Equals(id, StringComparison.Ordinal))
            throw new InvalidOperationException("Reconnect the Bangumi account.");
        var username = me.GetProperty("username").GetString()!;
        var games = new List<PlatformGame>();
        const int pageSize = 50;
        while (true)
        {
            var uri = new Uri(
                Api,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"users/{Uri.EscapeDataString(username)}/collections?subject_type=4&limit={pageSize}&offset={games.Count}"
                )
            );
            var page = await ReadAsync(client, uri, "Bearer", secret, null, token);
            var rows = page.GetProperty("data");
            foreach (var row in rows.EnumerateArray())
            {
                var subject = row.GetProperty("subject");
                var title = subject.GetProperty("name").GetString()!;
                games.Add(
                    new(row.GetProperty("subject_id").GetRawText(), title, Status(row.GetProperty("type").GetInt32()))
                );
            }
            if (games.Count >= page.GetProperty("total").GetInt32())
                return games;
            if (rows.GetArrayLength() == 0)
                throw new InvalidDataException("Bangumi stopped before the end of the collection.");
        }
    }

    private enum CollectionStatus
    {
        Wishlist = 1,
        Completed = 2,
        Playing = 3,
        OnHold = 4,
        Dropped = 5,
    }

    private static string Status(int value) =>
        (CollectionStatus)value switch
        {
            CollectionStatus.OnHold => "On hold",
            var status when Enum.IsDefined(status) => status.ToString(),
            _ => "Collected",
        };
}
