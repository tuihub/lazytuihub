using LazyTuiHub.Cli;
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
        var cli = args.Contains("--cli", StringComparer.Ordinal);
        if (cli)
            args = args.Where(value => !string.Equals(value, "--cli", StringComparison.Ordinal)).ToArray();
        using var options = new ConfigurationManager();
        options.AddEnvironmentVariables().AddCommandLine(args);
        if (options["endpoint"] is not null && (cli || options["command"] is not null))
        {
            Environment.ExitCode = await RemoteConsole.RunAsync(options, CancellationToken.None);
            return;
        }
        if (cli && options["command"] is null)
        {
            Console.WriteLine(RemoteConsole.Help);
            Console.WriteLine("Local: --command plugins|workflow|dao|games|rss|articles|access --action help");
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
