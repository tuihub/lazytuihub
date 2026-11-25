using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.Hosting;

namespace LazyTuiHub.Desktop;

public sealed partial class DesktopApp : Application
{
    private readonly IHost? host;
    public Styles DesignStyles { get; } = [];

    public DesktopApp() { }

    public DesktopApp(IHost host) => this.host = host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Insert(0, DesignStyles);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (host is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ShellWindow(host);
        base.OnFrameworkInitializationCompleted();
    }
}
