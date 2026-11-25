using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Sdk;
using Meziantou.Framework.RobotsTxt;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugin.TuiHub.Steam;

internal sealed class SteamStoreClient : IDisposable
{
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate = new(1);
    private RobotsFile? rules;
    private DateTimeOffset refreshAt;
    private DateTimeOffset lastRequest;
    private DateTimeOffset retryAt;

    public SteamStoreClient([FromKeyedServices(PluginProject.Id)] HttpMessageHandler transport)
    {
        client = new HttpClient(transport, disposeHandler: false);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(PluginHttpIdentity.UserAgent);
    }

    public async Task<JsonElement> ReadAsync(Uri uri, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow < retryAt)
                throw new HttpRequestException(
                    $"Steam requested a retry after {retryAt:u}.",
                    null,
                    HttpStatusCode.TooManyRequests
                );
            if (rules is null || DateTimeOffset.UtcNow >= refreshAt)
                rules = await LoadRulesAsync(new Uri(uri, "/robots.txt"), token);
            if (!rules.IsAllowed(PluginHttpIdentity.Product, uri.PathAndQuery))
                throw new HttpRequestException(
                    $"Steam robots.txt disallows {uri.AbsolutePath}.",
                    null,
                    HttpStatusCode.Forbidden
                );
            await WaitForAccessAsync(token);
            using var response = await client.GetAsync(uri, token);
            RequireSuccess(response);
            return await response.Content.ReadFromJsonAsync<JsonElement>(token);
        }
        catch (HttpRequestException error)
            when (error.StatusCode is null or HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError)
        {
            throw new InstanceUnavailableException($"{uri.Host}: {error.Message}", error);
        }
        catch (HttpRequestException error)
        {
            throw new PacketRejectedException($"{uri.Host}: {error.Message}", error);
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new InstanceUnavailableException($"Request to {uri.Host} timed out.", error);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<RobotsFile> LoadRulesAsync(Uri uri, CancellationToken token)
    {
        const int robotsRedirects = 5; // RFC 9309 §2.3.1.2
        for (var redirects = 0; redirects <= robotsRedirects; redirects++)
        {
            await WaitForAccessAsync(token);
            using var response = await client.GetAsync(uri, token);
            if (
                response.StatusCode
                    is HttpStatusCode.Moved
                        or HttpStatusCode.Redirect
                        or HttpStatusCode.SeeOther
                        or HttpStatusCode.TemporaryRedirect
                        or HttpStatusCode.PermanentRedirect
                && response.Headers.Location is { } location
            )
            {
                uri = new Uri(uri, location);
                continue;
            }
            var missing = response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone;
            if (!missing)
                RequireSuccess(response);
            var parsed = RobotsFile.Parse(missing ? string.Empty : await response.Content.ReadAsStringAsync(token));
            refreshAt = RulesExpiry(response);
            return parsed;
        }
        throw new HttpRequestException("Too many Steam robots.txt redirects.", null, HttpStatusCode.Redirect);
    }

    private async Task WaitForAccessAsync(CancellationToken token)
    {
        if (rules?.GetCrawlDelay(PluginHttpIdentity.Product) is { } interval)
        {
            var wait = lastRequest + interval - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, token);
        }
        lastRequest = DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset RulesExpiry(HttpResponseMessage response)
    {
        var now = DateTimeOffset.UtcNow;
        var policy = response.Headers.CacheControl;
        if (policy is { NoCache: true } or { NoStore: true })
            return now;
        var day = TimeSpan.FromDays(1); // RFC 9309 §2.4
        var issued = response.Headers.Date ?? now;
        var lifetime = policy?.MaxAge ?? (response.Content.Headers.Expires - issued) ?? day;
        var age = response.Headers.Age.GetValueOrDefault();
        if (now - issued > age)
            age = now - issued;
        return now + (lifetime < day ? lifetime : day) - age;
    }

    private void RequireSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode && response.Headers.RetryAfter is { } retry)
        {
            retryAt = retry.Date ?? DateTimeOffset.UtcNow + retry.Delta!.Value;
            throw new PacketRejectedException(
                $"Steam returned HTTP {(int)response.StatusCode}. Retry-After: {retry}.",
                new HttpRequestException(response.ReasonPhrase, null, response.StatusCode)
            );
        }
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        client.Dispose();
        gate.Dispose();
    }
}
