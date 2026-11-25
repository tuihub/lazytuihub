using Grpc.Core;
using LazyTuiHub.Client;
using LazyTuiHub.Interaction;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Cli;

public static class RemoteConsole
{
    public static async Task<int> RunAsync(IConfiguration config, CancellationToken token)
    {
        try
        {
            if (config["endpoint"] is not { } endpoint)
            {
                if (config["command"] is not null)
                    throw new ArgumentException("Provide an HTTPS endpoint.", nameof(config));
                Console.WriteLine(
                    "LazyTuiHub CLI\n--endpoint HTTPS_URL [GROUP ACTION]\nUse the application host with --cli for a local workspace."
                );
                return 0;
            }
            using var remote = new RemoteWorkspace(endpoint, config["Access:Session"], config["Access:Account"]);
            var services = new ServiceCollection();
            remote.Configure(
                services,
                config["plugin-directory"] ?? Path.Combine(AppContext.BaseDirectory, "plugins"),
                FrontendKind.Cli,
                Console.Error.WriteLine
            );
            await using var provider = services.BuildServiceProvider();
            return await CapabilityConsole.RunAsync(provider.GetRequiredService<CapabilityCatalog>(), config, token);
        }
        catch (Exception error)
            when (error
                    is SessionApiException
                        or IRemoteApiError
                        or RpcException
                        or HttpRequestException
                        or ArgumentException
                        or FormatException
                        or IOException
                        or InvalidOperationException
            )
        {
            await Console.Error.WriteLineAsync(error.Message.AsMemory(), token);
            return 1;
        }
    }
}
