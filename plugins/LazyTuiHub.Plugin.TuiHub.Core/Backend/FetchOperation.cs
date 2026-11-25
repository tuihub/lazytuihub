using System.Net;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Plugin.TuiHub.Core;

internal sealed class FetchOperation(HttpClient client, IReadOnlySet<string>? allowedOrigins)
{
    public async Task<HttpDocument> ExecuteAsync(FetchRequest request, CancellationToken token)
    {
        if (
            allowedOrigins is not null
            && (
                !request.Uri.IsAbsoluteUri
                || request.Uri.Scheme is not ("http" or "https")
                || !allowedOrigins.Contains(request.Uri.GetLeftPart(UriPartial.Authority))
            )
        )
            throw new InstanceUnavailableException("This instance does not serve that HTTP origin.");
        try
        {
            using var response = await client.GetAsync(request.Uri, HttpCompletionOption.ResponseContentRead, token);
            response.EnsureSuccessStatusCode();
            return new(request.Uri, await response.Content.ReadAsByteArrayAsync(token), request.Source);
        }
        catch (HttpRequestException error)
            when (error.StatusCode
                    is null
                        or HttpStatusCode.RequestTimeout
                        or HttpStatusCode.TooManyRequests
                        or >= HttpStatusCode.InternalServerError
            )
        {
            throw new InstanceUnavailableException(error.Message, error);
        }
        catch (HttpRequestException error) when (error.StatusCode is { } status)
        {
            throw new PacketRejectedException($"The upstream source returned HTTP {(int)status}.", error);
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new InstanceUnavailableException("HTTP fetch timed out.", error);
        }
    }
}
