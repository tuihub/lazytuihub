using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Api.Hosting;

public static partial class ApiErrors
{
    public static RpcException Create(StatusCode code, string reason, string message)
    {
        var status = new Google.Rpc.Status { Code = (int)code, Message = message };
        status.Details.Add(Any.Pack(new ErrorInfo { Reason = reason, Domain = "lazytuihub.tuihub.org" }));
        return new(
            new Grpc.Core.Status(code, message),
            new Metadata { { "grpc-status-details-bin", status.ToByteArray() } }
        );
    }

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> run,
        ILogger logger,
        Func<Exception, RpcException?>? translate = null
    )
    {
        try
        {
            return await run();
        }
        catch (WorkflowUnavailableException exception)
        {
            throw Create(StatusCode.FailedPrecondition, "WORKFLOW_UNAVAILABLE", exception.Message);
        }
        catch (PipelineExecutionException exception)
        {
            if (exception.InnerException is { } cause && translate?.Invoke(cause) is { } known)
                throw known;
            if (exception.InnerException is not KeyNotFoundException)
                LogPipelineFailure(logger, exception.NodeId, exception);
            throw exception.InnerException switch
            {
                PacketRejectedException rejected => Create(
                    StatusCode.FailedPrecondition,
                    "PACKET_REJECTED",
                    rejected.Message
                ),
                UnconfirmedExecutionException unconfirmed => Create(
                    StatusCode.Unknown,
                    "OUTCOME_UNKNOWN",
                    unconfirmed.Message
                ),
                InstanceUnavailableException unavailable => Create(
                    StatusCode.Unavailable,
                    "NO_AVAILABLE_INSTANCE",
                    unavailable.Message
                ),
                KeyNotFoundException => Create(
                    StatusCode.NotFound,
                    "RESOURCE_NOT_FOUND",
                    "The requested resource does not exist."
                ),
                _ => Create(
                    StatusCode.Unavailable,
                    "PIPELINE_FAILED",
                    "The pipeline failed. Check the server log before retrying."
                ),
            };
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pipeline failed at {NodeId}.")]
    private static partial void LogPipelineFailure(ILogger logger, string nodeId, Exception exception);
}
