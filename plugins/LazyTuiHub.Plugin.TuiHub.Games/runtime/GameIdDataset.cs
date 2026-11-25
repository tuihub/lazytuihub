using System.Text.Json;

namespace LazyTuiHub.Games;

public static class GameIdDataset
{
    public static async Task<JsonElement[]> FindAsync(Stream input, string provider, string id, CancellationToken token)
    {
        try
        {
            using var dataset = await JsonDocument.ParseAsync(input, cancellationToken: token);
            return dataset
                .RootElement.GetProperty("entries")
                .EnumerateArray()
                .Where(row => row.TryGetProperty(provider, out var ids) && ContainsId(ids, id))
                .Select(row => row.Clone())
                .ToArray();
        }
        catch (JsonException error)
        {
            throw new FormatException("The game ID dataset contains invalid JSON.", error);
        }
    }

    private static bool ContainsId(JsonElement value, string id) =>
        value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Any(item => ContainsId(item, id)),
            JsonValueKind.String => value.ValueEquals(id),
            JsonValueKind.Number => string.Equals(value.GetRawText(), id, StringComparison.Ordinal),
            _ => false,
        };
}
