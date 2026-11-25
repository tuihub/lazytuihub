using System.Runtime.CompilerServices;
using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Games;

namespace LazyTuiHub.Plugin.TuiHub.Steam;

internal static class GameSnapshot
{
    public static async Task<GameMetadata> LookupAsync(string path, string provider, string id, CancellationToken token)
    {
        return await ReadAsync(path, provider, token)
            .FirstOrDefaultAsync(item => string.Equals(id, item.ExternalId, StringComparison.Ordinal), token)
            ?? throw new KeyNotFoundException($"No {provider} record for '{id}'.");
    }

    public static async Task<GameMetadata[]> SearchAsync(
        string path,
        string provider,
        string query,
        CancellationToken token
    ) =>
        await ReadAsync(path, provider, token)
            .Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.AlternativeTitle?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
            )
            .ToArrayAsync(token);

    private static async IAsyncEnumerable<GameMetadata> ReadAsync(
        string path,
        string provider,
        [EnumeratorCancellation] CancellationToken token
    )
    {
        await foreach (var line in File.ReadLinesAsync(path, token))
        {
            var item = JsonSerializer.Deserialize<GameMetadata>(line, PipelineStore.JsonOptions);
            if (
                item is null
                || string.IsNullOrWhiteSpace(item.ExternalId)
                || string.IsNullOrWhiteSpace(item.Title)
                || !string.Equals(item.Provider, provider, StringComparison.Ordinal)
            )
                throw new InvalidDataException($"Invalid game metadata in the {provider} snapshot.");
            yield return item;
        }
    }
}
