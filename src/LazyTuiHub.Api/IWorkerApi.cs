using LazyTuiHub.Api.Execution.V1;

namespace LazyTuiHub.Api;

public interface IWorkerApi
{
    Task<DescribeWorkerResponse> DescribeWorkerAsync(CancellationToken cancellationToken = default);
    Task<ExecutePacketResponse> ExecutePacketAsync(
        ExecutePacketRequest request,
        CancellationToken cancellationToken = default
    );
}
