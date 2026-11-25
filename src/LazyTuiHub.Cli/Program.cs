using LazyTuiHub.Cli;
using Microsoft.Extensions.Configuration;

using var options = new ConfigurationManager();
try
{
    options.AddEnvironmentVariables().AddCommandLine(ConsoleArguments.Parse(args));
}
catch (ArgumentException error)
{
    await Console.Error.WriteLineAsync(error.Message);
    return 1;
}
return await CapabilityConsole.UntilCancelledAsync(token => RemoteConsole.RunAsync(options, token));
