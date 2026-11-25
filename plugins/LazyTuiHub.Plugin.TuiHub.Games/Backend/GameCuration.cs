using System.Globalization;
using System.Text.Json;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed class GameCuration(
    GameLibrary library,
    IWorkflowExecutor workflows,
    GameIdIndex index,
    IEnumerable<SqliteGameProvider> offline
)
{
    private ProviderOperations Provider(string name) => new(name, workflows);

    public Task<GameMetadata[]> SearchAsync(string provider, string query, CancellationToken token) =>
        Provider(provider).Search(query, token);

    public Task<GameMetadata> LookupAsync(CatalogKey key, CancellationToken token) =>
        Provider(key.Provider).Lookup(key.Id, token);

    public Task LinkAsync(string game, CatalogKey source, CancellationToken token) =>
        Provider(source.Provider).Import(source.Id, token, game);

    public async Task<CatalogKey[]> CandidatesAsync(string game, CancellationToken token)
    {
        var direct = Anchors(library.Show(game));
        return await Task.Run(
            () =>
                direct
                    .SelectMany(key => index.Match(key).Candidates)
                    .Distinct()
                    .OrderBy(key => key.Provider, StringComparer.Ordinal)
                    .ThenBy(key => key.Id, StringComparer.Ordinal)
                    .ToArray(),
            token
        );
    }

    public async Task<int> FillAsync(string game, CancellationToken token)
    {
        var detail = library.Show(game);
        var available = offline.Select(c => c.AvailableCatalog).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var keys = await Task.Run(
            () =>
                Anchors(detail)
                    .SelectMany(key =>
                    {
                        var match = index.Match(key);
                        return match.Unique ? match.Candidates : [key];
                    })
                    .Distinct()
                    .Where(key => available.Contains(key.Provider))
                    .ToArray(),
            token
        );
        var count = 0;
        foreach (var group in keys.GroupBy(key => key.Provider, StringComparer.Ordinal))
        {
            if (
                group.Count() != 1
                || detail.Sources.Any(source => source.Provider.Equals(group.Key, StringComparison.Ordinal))
            )
                continue;
            await LinkAsync(game, group.Single(), token);
            count++;
        }
        return count;
    }

    private static CatalogKey[] Anchors(GameDetails detail) =>
        detail
            .Availability.Select(item => new CatalogKey(item.Provider, item.SourceId))
            .Concat(detail.Sources.Select(item => new CatalogKey(item.Provider, item.ExternalId)))
            .Where(key => key.Provider is not "folder")
            .Distinct()
            .ToArray();

    internal static IEnumerable<CatalogKey> RowKeys(JsonElement row)
    {
        foreach (var field in row.EnumerateObject())
        {
            if (field.Name is not ("steam" or "vndb" or "bangumi"))
                continue;
            var values =
                field.Value.ValueKind == JsonValueKind.Array ? field.Value.EnumerateArray().ToArray() : [field.Value];
            foreach (var value in values)
            {
                var id = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    _ => null,
                };
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (
                    field.NameEquals("vndb")
                    && ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                )
                    id = "v" + id;
                yield return new(field.Name, id);
            }
        }
    }
}
