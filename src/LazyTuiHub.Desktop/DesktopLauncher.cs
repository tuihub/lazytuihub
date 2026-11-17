using Avalonia;
using Microsoft.Extensions.Hosting;

namespace LazyTuiHub.Desktop;

public static class DesktopLauncher
{
    public static int Run(IHost host, string[] args)
    {
        host.StartAsync().GetAwaiter().GetResult();
        try
        {
            return AppBuilder
                .Configure(() => new DesktopApp(host))
                .UsePlatformDetect()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
