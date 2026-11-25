using Grpc.Core;
using LazyTuiHub.Framework;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.Logging;
using static LazyTuiHub.Api.RpcErrors;

namespace LazyTuiHub.Api.Hosting;

public static partial class ApiErrors
{
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
        catch (AccessRequiredException exception)
        {
            throw Create(StatusCode.Unauthenticated, "SESSION_REQUIRED", exception.Message);
        }
        catch (AccessDeniedException exception)
        {
            throw Create(StatusCode.PermissionDenied, "ACCESS_DENIED", exception.Message);
        }
        catch (KeyNotFoundException)
        {
            throw Create(StatusCode.NotFound, "RESOURCE_NOT_FOUND", "The requested resource does not exist.");
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
                AccessRequiredException required => Create(
                    StatusCode.Unauthenticated,
                    "SESSION_REQUIRED",
                    required.Message
                ),
                AccessDeniedException denied => Create(StatusCode.PermissionDenied, "ACCESS_DENIED", denied.Message),
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
