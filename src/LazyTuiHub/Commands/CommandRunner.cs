using System.Data.Common;
using Grpc.Core;
using LazyTuiHub.Cli;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Commands;

internal static class CommandRunner
{
    public static async Task<int> RunAsync(string[] args, CancellationToken token)
    {
        try
        {
            using var host = ApplicationHost.CreateCli(args);
            return await CapabilityConsole.RunAsync(
                host.Services.GetRequiredService<CapabilityCatalog>(),
                host.Services.GetRequiredService<IConfiguration>(),
                token
            );
        }
        catch (Exception error)
            when (error
                    is LazyTuiHub.Framework.PipelineExecutionException
                        or DbException
                        or RpcException
                        or ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or System.Text.Json.JsonException
                        or HttpRequestException
                        or FormatException
                        or IOException
            )
        {
            await Console.Error.WriteLineAsync(error.Message.AsMemory(), CancellationToken.None);
            return 1;
        }
    }
}
