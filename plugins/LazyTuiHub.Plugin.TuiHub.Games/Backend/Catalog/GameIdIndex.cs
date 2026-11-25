using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed class GameIdIndex(IConfiguration config)
{
    private readonly Lock gate = new();
    private Dictionary<CatalogKey, List<CatalogKey[]>> index = [];
    private (string Path, DateTime Modified, long Size)? loaded;

    internal (CatalogKey[] Candidates, bool Unique) Match(CatalogKey key)
    {
        var path = config["Games:IdDataset"];
        if (string.IsNullOrEmpty(path))
            return ([key], true);
        var file = new FileInfo(path);
        lock (gate)
        {
            var stamp = (file.FullName, file.LastWriteTimeUtc, file.Length);
            if (loaded != stamp)
            {
                using var stream = file.OpenRead();
                using var data = JsonDocument.Parse(stream);
                var next = new Dictionary<CatalogKey, List<CatalogKey[]>>();
                foreach (var row in data.RootElement.GetProperty("entries").EnumerateArray())
                {
                    var keys = GameCuration.RowKeys(row).Distinct().ToArray();
                    foreach (var source in keys)
                    {
                        if (!next.TryGetValue(source, out var entries))
                            next[source] = entries = [];
                        entries.Add(keys);
                    }
                }
                index = next;
                loaded = stamp;
            }
            if (!index.TryGetValue(key, out var found))
                return ([key], true);
            var candidates = found.SelectMany(row => row).Distinct().ToArray();
            return (
                candidates,
                found.Count == 1
                    && candidates.Select(item => item.Provider).Distinct(StringComparer.Ordinal).Count()
                        == candidates.Length
                    && candidates.All(item => index[item].Count == 1)
            );
        }
    }
}
