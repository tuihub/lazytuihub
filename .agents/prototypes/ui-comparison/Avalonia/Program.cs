using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using UiComparison;
using UiComparison.AvaloniaApp;

Measurements.Start();
var verify = args.Contains("--verify", StringComparer.Ordinal);
PrototypeApp.WithoutPlugin = args.Contains("--without-plugin", StringComparer.Ordinal);
var builder = AppBuilder.Configure<PrototypeApp>();
if (verify)
{
    builder.UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
    Measurements.VerifySession();
    var window = new ReaderWindow();
    window.Show();
    Dispatcher.UIThread.RunJobs();
    window.Verify();
    window.Close();
}
else
    builder.UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
