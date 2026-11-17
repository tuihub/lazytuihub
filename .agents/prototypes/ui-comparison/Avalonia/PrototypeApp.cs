using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace UiComparison.AvaloniaApp;

public sealed class PrototypeApp : Application
{
    public static bool WithoutPlugin { get; set; }

    public override void Initialize()
    {
        Name = "LazyTuiHub · Avalonia";
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ReaderWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
