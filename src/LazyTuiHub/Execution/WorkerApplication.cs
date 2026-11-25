using System.Net;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Execution.V1;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Execution;

internal sealed partial class WorkerApplication(
    IConfiguration configuration,
    WorkflowRuntime workflows,
    ILogger<WorkerApplication> logger
) : IWorkerApi
{
    private readonly string instance = ClusterConfiguration.Required(configuration.GetSection("Cluster"), "Id");
    private readonly HashSet<string> exports = ClusterConfiguration.Set(configuration.GetSection("Cluster"), "Export");

    private readonly HashSet<string> resources = ClusterConfiguration.Set(
        configuration.GetSection("Cluster"),
        "Resources"
    );

    public Task<DescribeWorkerResponse> DescribeWorkerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new DescribeWorkerResponse
            {
                Instance = instance,
                Resources = { resources },
                RepeatableOperations =
                {
                    workflows.Registry.PacketOperations(PacketExecutionMode.Repeatable).Where(exports.Contains),
                },
                OwnedOperations =
                {
                    workflows.Registry.PacketOperations(PacketExecutionMode.Owned).Where(exports.Contains),
                },
            }
        );
    }

    public async Task<ExecutePacketResponse> ExecutePacketAsync(
        ExecutePacketRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!string.Equals(request.Instance, instance, StringComparison.Ordinal))
            throw RpcErrors.Create(
                StatusCode.FailedPrecondition,
                "INSTANCE_MISMATCH",
                "The requested execution instance does not match this worker."
            );
        if (!exports.Contains(request.NodeType))
            throw RpcErrors.Create(
                StatusCode.PermissionDenied,
                "NOT_EXPORTED",
                "This worker does not export the requested operation."
            );
        if (!request.RequiredResources.All(resources.Contains))
            throw RpcErrors.Create(
                StatusCode.Unavailable,
                "RESOURCE_UNAVAILABLE",
                "This instance no longer offers the required resources."
            );
        try
        {
            var settings = JsonSerializer.Deserialize<JsonElement>(request.Settings.Span);
            if (settings.ValueKind != JsonValueKind.Object)
                throw new JsonException("Node settings must be an object.");
            var output = await workflows.Registry.InvokePacketAsync(
                request.NodeType,
                request.Repeatable ? PacketExecutionMode.Repeatable : PacketExecutionMode.Owned,
                resources,
                request.Input.ToByteArray(),
                settings,
                cancellationToken
            );
            LogCompletion(logger, instance, request.NodeType);
            return new() { Output = ByteString.CopyFrom(output) };
        }
        catch (InstanceUnavailableException error)
        {
            throw RpcErrors.Create(StatusCode.Unavailable, "INSTANCE_UNAVAILABLE", error.Message);
        }
        catch (JsonException)
        {
            throw RpcErrors.Create(
                StatusCode.InvalidArgument,
                "INVALID_PACKET",
                "The packet or settings do not match this operation."
            );
        }
        catch (PacketRejectedException error)
        {
            throw RpcErrors.Create(StatusCode.FailedPrecondition, "UPSTREAM_REJECTED", error.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Instance {Instance} executed {Operation}.")]
    private static partial void LogCompletion(ILogger logger, string instance, string operation);
}
