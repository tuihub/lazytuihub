using LazyTuiHub.Client;
using LazyTuiHub.Interaction;
using Microsoft.Extensions.Configuration;

namespace LazyTuiHub.Cli;

public static class CapabilityConsole
{
    public static async Task<int> UntilCancelledAsync(Func<CancellationToken, Task<int>> execute)
    {
        using var stop = new CancellationTokenSource();
        void Cancel(object? sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            stop.Cancel();
        }
        Console.CancelKeyPress += Cancel;
        try
        {
            return await execute(stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            const int interrupted = 130;
            return interrupted;
        }
        finally
        {
            Console.CancelKeyPress -= Cancel;
        }
    }

    public static async Task<int> RunAsync(CapabilityCatalog catalog, IConfiguration options, CancellationToken token)
    {
        if (options["command"] is not { } name)
        {
            Console.WriteLine("LazyTuiHub CLI\nGROUP ACTION [options]\nAvailable groups:");
            foreach (var item in catalog.Groups)
                Console.WriteLine("  " + item.Name);
            return 0;
        }
        var group = catalog.Get(name);
        if (options["action"] is null or "help")
        {
            foreach (var action in group.Actions)
            {
                Help(action);
            }
            return 0;
        }
        var operation = group.Get(options["action"]!);
        if (options["cli-help"] is "true")
        {
            Help(operation);
            return 0;
        }
        var result = await operation.InvokeAsync(
            key => options[key],
            key => ReadSecret(key, options),
            new(value => Console.WriteLine(OperationFormatting.Json(value)), Console.Error.WriteLine),
            token
        );
        if (result is UnsuccessfulResult failure)
        {
            Console.WriteLine(OperationFormatting.Json(failure.Detail));
            return 1;
        }
        if (result is not null)
            Console.WriteLine(OperationFormatting.Json(result));
        return 0;
    }

    private static void Help(Capability action)
    {
        var inputs = action.Parameters.Where(p => p.IsInput).Select(Usage);
        Console.WriteLine($"{action.Group.Name} {action.Name} {string.Join(' ', inputs)}\n  {action.Description}");
    }

    private static string Usage(CapabilityParameter parameter)
    {
        if (parameter.IsSecret)
            return $"({parameter.Name} via prompt or stdin)";
        return parameter.Optional ? $"[--{parameter.Name} VALUE]" : $"--{parameter.Name} VALUE";
    }

    private static string ReadSecret(string name, IConfiguration config)
    {
        if (name is "password" && config["Access:Password"] is { } supplied)
            return supplied;
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? throw new EndOfStreamException($"Supply {name} on stdin.");
        Console.Error.Write($"{name}: ");
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return buffer.ToString();
            }
            if (key.Key == ConsoleKey.Backspace && buffer.Length > 0)
                buffer.Length--;
            else if (!char.IsControl(key.KeyChar))
                buffer.Append(key.KeyChar);
        }
    }
}
