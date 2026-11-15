using LazyTuiHub.Api;
using LazyTuiHub.Api.Greeting.V1;
using LazyTuiHub.Client;

namespace LazyTuiHub.Examples;

internal static class GreetingDemo
{
    public static async Task RunAsync(WebApplication app)
    {
        await app.StartAsync(app.Lifetime.ApplicationStopping);
        try
        {
            var cancellationToken = app.Lifetime.ApplicationStopping;
            var local = app.Services.GetRequiredService<IGreetingApi>();
            using var httpClient = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var remote = new GreetingHttpClient(httpClient);

            foreach (var recipient in new[] { string.Empty, "Ada", "世界" })
            {
                var request = new GenerateGreetingRequest { Recipient = recipient };
                var localReply = await local.GenerateGreetingAsync(request, cancellationToken);
                var remoteReply = await remote.GenerateGreetingAsync(request, cancellationToken);
                if (!localReply.Equals(remoteReply))
                {
                    throw new InvalidOperationException("Local and HTTP greetings differ.");
                }

                Console.WriteLine($"Local = HTTP: {localReply.Message}");
            }
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
        }
    }
}
