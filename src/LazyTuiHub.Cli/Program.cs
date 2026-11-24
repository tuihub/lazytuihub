using LazyTuiHub.Cli;
using Microsoft.Extensions.Configuration;

using var options = new ConfigurationManager();
options.AddEnvironmentVariables().AddCommandLine(args);
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    stop.Cancel();
};
try
{
    return await RemoteConsole.RunAsync(options, stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    const int interrupted = 130;
    return interrupted;
}
