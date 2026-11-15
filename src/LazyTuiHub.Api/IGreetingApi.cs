using LazyTuiHub.Api.Greeting.V1;

namespace LazyTuiHub.Api;

public interface IGreetingApi
{
    Task<GenerateGreetingResponse> GenerateGreetingAsync(
        GenerateGreetingRequest request,
        CancellationToken cancellationToken = default
    );
}
