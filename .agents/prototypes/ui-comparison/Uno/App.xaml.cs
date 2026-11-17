using Microsoft.UI.Xaml;

namespace UiComparison.UnoApp;

public sealed partial class App : Application
{
    public static bool VerifyMode { get; set; }
    public static bool WithoutPlugin { get; set; }
    public static bool Cupertino { get; set; }
    private Window? window;

    public App()
    {
        InitializeComponent();
        if (Cupertino)
        {
            Resources.MergedDictionaries.Add(new Uno.Cupertino.CupertinoColors());
            Resources.MergedDictionaries.Add(new Uno.Cupertino.CupertinoFonts());
            Resources.MergedDictionaries.Add(new Uno.Cupertino.CupertinoResources { WithImplicitStyles = true });
        }
    }

    private static async Task VerifyAndExitAsync(ReaderView reader)
    {
        try
        {
            Measurements.VerifySession();
            await reader.VerifyAsync();
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Environment.Exit(1);
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var reader = new ReaderView();
        window = new Window
        {
            Title = $"LazyTuiHub · Uno{(Cupertino ? " Cupertino" : "")} prototype",
            Content = reader,
        };
        var appWindow = window.AppWindow;
        reader.Loaded += (_, _) =>
        {
            var scale = reader.XamlRoot!.RasterizationScale;
            appWindow.Resize(new Windows.Graphics.SizeInt32((int)(1180 * scale), (int)(800 * scale)));
            Measurements.Save(
                "uno",
                "loaded",
                new
                {
                    Theme = Cupertino ? "Cupertino" : "Fluent",
                    RasterizationScale = scale,
                    RequestedWidthDip = 1180,
                    RequestedHeightDip = 800,
                }
            );
            if (VerifyMode)
                _ = VerifyAndExitAsync(reader);
        };
        window.Activate();
    }
}
