using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LazyTuiHub.Plugin.TuiHub.Games;

public abstract class GameAccountProvider
{
    public abstract string Id { get; }
    public abstract Uri SignInPage { get; }
    public virtual bool UsesToken => true;
    public abstract Task<PlatformIdentity> IdentifyAsync(
        HttpClient client,
        string secret,
        Action<Uri> showBrowser,
        CancellationToken token
    );
    public abstract Task<IReadOnlyList<PlatformGame>> LibraryAsync(
        HttpClient client,
        string id,
        string secret,
        CancellationToken token
    );

    protected static async Task<JsonElement> ReadAsync(
        HttpClient client,
        Uri uri,
        string header,
        string secret,
        object? query,
        CancellationToken token
    )
    {
        using var request = new HttpRequestMessage(query is null ? HttpMethod.Get : HttpMethod.Post, uri);
        if (header.Equals("x-webapi-key", StringComparison.Ordinal))
            request.Headers.Add(header, secret);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue(header, secret);
        if (query is not null)
            request.Content = JsonContent.Create(query);
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(token);
    }
}
