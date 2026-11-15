using LazyTuiHub.Api;
using LazyTuiHub.Api.Greeting.V1;

namespace LazyTuiHub.Application;

internal sealed class GreetingApplication : IGreetingApi
{
    public Task<GenerateGreetingResponse> GenerateGreetingAsync(
        GenerateGreetingRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recipient = string.IsNullOrEmpty(request.Recipient) ? "World" : request.Recipient;
        return Task.FromResult(new GenerateGreetingResponse { Message = $"Hello, {recipient}!" });
    }
}
