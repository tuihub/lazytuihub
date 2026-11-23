using System.Data.Common;
using Grpc.Core;
using LazyTuiHub.Access;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Commands;

internal static class CommandRunner
{
    public static async Task RunAsync(WebApplication app, string command)
    {
        ConsoleCancelEventHandler cancel = (_, args) =>
        {
            args.Cancel = true;
            app.Lifetime.StopApplication();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            var action = app.Configuration["action"] ?? "help";
            if (string.Equals(command, "access", StringComparison.Ordinal))
            {
                AccessCommand.Run(app, action);
                return;
            }
            using var access = ApplicationHost.EnterLocal(app.Services, app.Configuration);
            if (command is "dao" or "plugins" or "workflow")
                app.Services.GetRequiredService<ResourceAccess>()
                    .Require(AccountAuthority.OperationsUri, AccessMode.Write);
            var token = app.Lifetime.ApplicationStopping;
            switch (command)
            {
                case "dao":
                    DaoCommand.Run(app, action, token);
                    break;
                case "plugins":
                    await PluginCommand.RunAsync(app, action, token);
                    break;
                case "workflow":
                    await WorkflowCommand.RunAsync(app, action, token);
                    break;
                default:
                    await app.Services.GetRequiredService<PluginRuntime>().Command(command).Run(app, action, token);
                    break;
            }
        }
        catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            Console.WriteLine("Stopped.");
        }
        catch (Exception error)
            when (error
                    is LazyTuiHub.Framework.PipelineExecutionException
                        or DbException
                        or RpcException
                        or ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or FormatException
                        or IOException
            )
        {
            await Console.Error.WriteLineAsync(error.Message.AsMemory(), CancellationToken.None);
            Environment.ExitCode = 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }
}
