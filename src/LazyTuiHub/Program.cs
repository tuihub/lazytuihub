using LazyTuiHub.Client;
using LazyTuiHub.Commands;
using LazyTuiHub.Desktop;
using LazyTuiHub.Examples;

namespace LazyTuiHub;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--desktop", StringComparer.Ordinal))
        {
            var options = args.Where(value => !string.Equals(value, "--desktop", StringComparison.Ordinal)).ToArray();
            using var host = ApplicationHost.CreateDesktop(options);
            return DesktopLauncher.Run(host, options);
        }
        RunAsync(args).GetAwaiter().GetResult();
        return Environment.ExitCode;
    }

    private static async Task RunAsync(string[] args)
    {
        if (args.Contains("--workflow-demo", StringComparer.Ordinal))
        {
            await WorkflowDemo.RunAsync();
            return;
        }
        if (args.Contains("--rss-demo", StringComparer.Ordinal))
        {
            await RssDemo.RunAsync();
            return;
        }
        using var options = new ConfigurationManager();
        options.AddEnvironmentVariables().AddCommandLine(args);
        if (
            string.Equals(options["command"], "access", StringComparison.Ordinal) && options["endpoint"] is { } endpoint
        )
        {
            try
            {
                await AccessCommand.RunRemoteAsync(options, endpoint, CancellationToken.None);
            }
            catch (Exception error) when (error is SessionApiException or HttpRequestException or ArgumentException)
            {
                await Console.Error.WriteLineAsync(error.Message.AsMemory(), CancellationToken.None);
                Environment.ExitCode = 1;
            }
            return;
        }
        await using var app = ApplicationHost.Create(args);
        if (app.Configuration.GetValue<bool>("demo"))
            await GreetingDemo.RunAsync(app);
        else if (app.Configuration["command"] is { } command)
            await CommandRunner.RunAsync(app, command);
        else
            await app.RunAsync();
    }
}
