using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using LazyTuiHub.Access;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Execution.V1;
using LazyTuiHub.Api.Hosting;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Execution;

internal sealed class WorkerEndpoint(IWorkerApi application, ResourceAccess access, WorkflowRuntime workflows)
    : WorkerService.WorkerServiceBase
{
    public override Task<DescribeWorkerResponse> DescribeWorker(
        DescribeWorkerRequest request,
        ServerCallContext context
    ) => application.DescribeWorkerAsync(context.CancellationToken);

    public override async Task<ExecutePacketResponse> ExecutePacket(
        ExecutePacketRequest request,
        ServerCallContext context
    )
    {
        var account = context.GetHttpContext().Request.Headers[AccessHosting.AccountHeader].FirstOrDefault();
        if (access.IsSingleAccount != (account is null))
            throw ApiErrors.Create(
                StatusCode.FailedPrecondition,
                "ACCOUNT_SCOPE_MISMATCH",
                "The coordinator and worker must agree on account-scoped execution."
            );
        if (account is null)
            return await application.ExecutePacketAsync(request, context.CancellationToken);
        if (!ResourceUri.IsAccountId(account))
            throw ApiErrors.Create(
                StatusCode.InvalidArgument,
                "INVALID_ACCOUNT",
                "The assignment has an invalid account."
            );
        var data = workflows.Registry.DataAccess(request.NodeType);
        IReadOnlyList<UriGrant> grants = data is null ? [] : [new(data.UriFor(account), data.Mode)];
        using var assignment = access.Enter(new(request.Instance, account, () => grants));
        return await application.ExecutePacketAsync(request, context.CancellationToken);
    }

    public static void Map(WebApplication app)
    {
        var cluster = app.Configuration.GetSection("Cluster");
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(ClusterConfiguration.Required(cluster, "Token")));
        var trustedHttp = cluster.GetValue<bool>("AllowInsecureHttp");
        app.Use(
            async (context, next) =>
            {
                if (!trustedHttp && !context.Request.IsHttps)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                if (
                    !AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var auth)
                    || !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                    || auth.Parameter is null
                    || !CryptographicOperations.FixedTimeEquals(
                        expected,
                        SHA256.HashData(Encoding.UTF8.GetBytes(auth.Parameter))
                    )
                )
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                await next(context);
            }
        );
        app.MapGrpcService<WorkerEndpoint>();
    }
}
