using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Execution.V1;

namespace LazyTuiHub.Execution;

internal sealed class WorkerEndpoint(IWorkerApi application) : WorkerService.WorkerServiceBase
{
    public override Task<DescribeWorkerResponse> DescribeWorker(
        DescribeWorkerRequest request,
        ServerCallContext context
    ) => application.DescribeWorkerAsync(context.CancellationToken);

    public override Task<ExecutePacketResponse> ExecutePacket(
        ExecutePacketRequest request,
        ServerCallContext context
    ) => application.ExecutePacketAsync(request, context.CancellationToken);

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
