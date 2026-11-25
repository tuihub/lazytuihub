using Microsoft.Extensions.Caching.Memory;

namespace LazyTuiHub.Plugin.TuiHub.Games.Desktop;

internal sealed class GameCovers(HttpClient client) : IDisposable
{
    // Cache encoded artwork so recycled cards and details can reuse it without holding Skia bitmaps.
    // The byte budget applies only to reuse. Larger images can still be displayed.
    private const long CacheBytes = 16 * 1024 * 1024;
    private readonly MemoryCache recent = new(new MemoryCacheOptions { SizeLimit = CacheBytes });

    public async Task<byte[]> ReadAsync(Uri uri, CancellationToken token)
    {
        if (recent.TryGetValue(uri, out byte[]? cached) && cached is not null)
            return cached;
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        token.ThrowIfCancellationRequested();
        var policy = response.Headers.CacheControl;
        if (policy is not { NoStore: true } && policy is not { NoCache: true } && bytes.LongLength <= CacheBytes)
        {
            var options = new MemoryCacheEntryOptions { Size = bytes.LongLength };
            if (policy?.MaxAge is { } lifetime)
                options.AbsoluteExpiration = (response.Headers.Date ?? DateTimeOffset.UtcNow) + lifetime;
            else
                options.AbsoluteExpiration = response.Content.Headers.Expires;
            recent.Set(uri, bytes, options);
        }
        return bytes;
    }

    public void Dispose() => recent.Dispose();
}
