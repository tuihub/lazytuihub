using UiComparison;
using UiComparison.UnoApp;
using Uno.UI.Hosting;

Measurements.Start();
App.VerifyMode = args.Contains("--verify", StringComparer.Ordinal);
App.WithoutPlugin = args.Contains("--without-plugin", StringComparer.Ordinal);
App.Cupertino = args.Contains("--cupertino", StringComparer.Ordinal);
var builder = UnoPlatformHostBuilder.Create().App(() => new App());
var host = App.VerifyMode
    ? builder.UseHeadless(options => options.WithSize(1180, 800)).Build()
    : builder.UseMacOS().UseWin32().UseX11().Build();
#pragma warning disable MA0042, S6966 // The native AppKit event loop starts on the main thread.
host.Run();
#pragma warning restore MA0042, S6966
