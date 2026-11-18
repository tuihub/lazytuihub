using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Google.Protobuf;
using LazyTuiHub.Api.Execution.V1;
using LazyTuiHub.Client;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Execution;

internal sealed class WorkerPeer(Uri address, string credential, IHttpClientFactory clients) : IExecutionPeer
{
    public string Endpoint => address.AbsoluteUri;

    public Task<WorkerInventory> InspectAsync(CancellationToken token) =>
        RequestAsync(
            async api =>
            {
                var report = await api.DescribeWorkerAsync(token);
                return new WorkerInventory(
                    report.Instance,
                    report.Resources.ToHashSet(StringComparer.Ordinal),
                    report.RepeatableOperations.ToHashSet(StringComparer.Ordinal),
                    report.OwnedOperations.ToHashSet(StringComparer.Ordinal)
                );
            },
            token,
            inspecting: true
        );

    public Task<byte[]> InvokeAsync(
        string instance,
        string operation,
        IReadOnlyList<string> requirements,
        PacketExecutionMode mode,
        JsonElement settings,
        byte[] input,
        CancellationToken token
    ) =>
        RequestAsync(
            async api =>
            {
                var result = await api.ExecutePacketAsync(
                    new ExecutePacketRequest
                    {
                        Instance = instance,
                        RequiredResources = { requirements },
                        Repeatable = mode == PacketExecutionMode.Repeatable,
                        NodeType = operation,
                        Settings = ByteString.CopyFromUtf8(settings.GetRawText()),
                        Input = ByteString.CopyFrom(input),
                    },
                    token
                );
                return result.Output.ToByteArray();
            },
            token,
            ownerWrite: mode == PacketExecutionMode.Owned
        );

    private async Task<T> RequestAsync<T>(
        Func<WorkerHttpClient, Task<T>> request,
        CancellationToken token,
        bool inspecting = false,
        bool ownerWrite = false
    )
    {
        using var http = clients.CreateClient(nameof(WorkerPeer));
        http.BaseAddress = address;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        try
        {
            return await request(new WorkerHttpClient(http));
        }
        catch (WorkerApiException error)
            when (inspecting
                || ownerWrite
                    && (
                        error.StatusCode == (int)HttpStatusCode.OK
                        || error.StatusCode >= (int)HttpStatusCode.InternalServerError
                    )
                || error.StatusCode
                    is (int)HttpStatusCode.ServiceUnavailable
                        or (int)HttpStatusCode.BadGateway
                        or (int)HttpStatusCode.GatewayTimeout
            )
        {
            throw new InstanceUnavailableException(ResponseReason(error), error);
        }
        catch (WorkerApiException error)
        {
            throw new PacketRejectedException(ResponseReason(error), error);
        }
        catch (HttpRequestException error)
        {
            throw new InstanceUnavailableException(error.Message, error);
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new InstanceUnavailableException("Worker request timed out.", error);
        }
    }

    private static string ResponseReason(WorkerApiException error)
    {
        if (error.Response is { Length: > 0 } body)
            try
            {
                using var json = JsonDocument.Parse(body);
                if (
                    json.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String
                )
                    return message.GetString()!;
            }
            catch (JsonException)
            {
                return $"Worker returned a non-JSON HTTP {error.StatusCode} response.";
            }
        return $"Worker returned HTTP {error.StatusCode}.";
    }
}
