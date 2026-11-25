using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Greeting.V1;
using GreetingContract = LazyTuiHub.Api.Greeting.V1.GreetingService;

namespace LazyTuiHub.Plugin.TuiHub.Core;

internal sealed class GreetingService(IGreetingApi application) : GreetingContract.GreetingServiceBase
{
    public override Task<GenerateGreetingResponse> GenerateGreeting(
        GenerateGreetingRequest request,
        ServerCallContext context
    ) => application.GenerateGreetingAsync(request, context.CancellationToken);
}
