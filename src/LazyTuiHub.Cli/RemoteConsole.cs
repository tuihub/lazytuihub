using LazyTuiHub.Client;
using Microsoft.Extensions.Configuration;

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
            return await CapabilityConsole.RunAsync(remote.Catalog, config, token);
        }
        catch (Exception error)
            when (error
                    is SessionApiException
                        or ArticleApiException
                        or RssApiException
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
