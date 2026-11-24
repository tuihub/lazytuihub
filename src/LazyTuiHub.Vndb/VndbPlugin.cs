using System.Text.Json;
using LazyTuiHub.Games;

namespace LazyTuiHub.Vndb;

public sealed class VndbPlugin : GameProviderPlugin
{
    private static readonly Uri Endpoint = new("https://api.vndb.org/kana/vn");

    protected override Task<GameMetadata[]> SearchAsync(HttpClient client, string query, CancellationToken token) =>
        QueryAsync(client, "search", query, "searchrank", token);

    protected override async Task<GameMetadata> LookupAsync(HttpClient client, string id, CancellationToken token)
    {
        if (!id.StartsWith('v'))
            throw new ArgumentException("Provide a VNDB ID such as v17.", nameof(id));
        _ = NumericId(id[1..]);
        var rows = await QueryAsync(client, "id", id, "id", token);
        return rows.SingleOrDefault() ?? throw new KeyNotFoundException($"No VNDB entry for '{id}'.");
    }

    private static async Task<GameMetadata[]> QueryAsync(
        HttpClient client,
        string filter,
        string value,
        string sort,
        CancellationToken token
    )
    {
        var result = await RequestAsync(
            client,
            Endpoint,
            new
            {
                filters = new[] { filter, "=", value },
                fields = "title,alttitle,description,image.url,released,developers.name",
                sort,
            },
            token
        );
        return result.GetProperty("results").EnumerateArray().Select(Map).ToArray();
    }

    private static GameMetadata Map(JsonElement row)
    {
        var id = row.GetProperty("id").GetString()!;
        return new(
            "vndb",
            id,
            row.GetProperty("title").GetString()!,
            "https://vndb.org/" + id,
            Text(row, "description"),
            Text(row.GetProperty("image"), "url"),
            Text(row, "released"),
            Text(row, "alttitle"),
            row.GetProperty("developers")
                .EnumerateArray()
                .Select(item => item.GetProperty("name").GetString()!)
                .ToArray()
        );
    }
}
