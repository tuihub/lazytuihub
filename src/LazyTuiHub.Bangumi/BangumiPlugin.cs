using System.Text.Json;
using LazyTuiHub.Games;

namespace LazyTuiHub.Bangumi;

public sealed class BangumiPlugin : GameProviderPlugin
{
    private static readonly Uri Api = new("https://api.bgm.tv/v0/");
    private const int GameType = 4;

    protected override async Task<GameMetadata[]> SearchAsync(HttpClient client, string query, CancellationToken token)
    {
        var result = await RequestAsync(
            client,
            new Uri(Api, "search/subjects"),
            new
            {
                keyword = query,
                sort = "match",
                filter = new { type = new[] { GameType } },
            },
            token
        );
        return result.GetProperty("data").EnumerateArray().Select(Map).ToArray();
    }

    protected override async Task<GameMetadata> LookupAsync(HttpClient client, string id, CancellationToken token)
    {
        var row = await RequestAsync(client, new Uri(Api, "subjects/" + NumericId(id)), null, token);
        if (row.GetProperty("type").GetInt32() != GameType)
            throw new InvalidOperationException("This Bangumi subject is not a game.");
        return Map(row);
    }

    private static GameMetadata Map(JsonElement row)
    {
        var id = row.GetProperty("id").GetRawText();
        return new(
            "bangumi",
            id,
            row.GetProperty("name").GetString()!,
            "https://bgm.tv/subject/" + id,
            Text(row, "summary"),
            row.TryGetProperty("images", out var images) ? Text(images, "common") : null,
            Text(row, "date"),
            Text(row, "name_cn")
        );
    }
}
