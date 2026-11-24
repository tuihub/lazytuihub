using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Desktop;

public sealed partial class ShellWindow
{
    private bool appearanceReady;

    public ShellWindow ChangeAppearance(AppearancePreferences preferences)
    {
        var app = (DesktopApp)Application.Current!;
        if (!appearance.Change(app, preferences))
        {
            ShowAppearance();
            return this;
        }
        var replacement = new ShellWindow(host, Pages.SelectedIndex)
        {
            Width = Width,
            Height = Height,
            Position = Position,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowState = WindowState,
            ShowActivated = ShowActivated,
        };
        if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = replacement;
        replacement.Show();
        replaced = true;
        Close();
        return replacement;
    }

    private void ShowAppearance()
    {
        appearanceReady = false;
        DesignPicker.SelectedIndex = (int)appearance.Preferences.Design;
        ColorPicker.SelectedIndex = (int)appearance.Preferences.Color;
        MotionPicker.IsChecked = appearance.Preferences.ReduceMotion;
        AppearanceStatus.Text = appearance.Status;
        appearanceReady = true;
    }

    private void AppearanceChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!appearanceReady)
            return;
        try
        {
            ChangeAppearance(
                new(
                    (DesignSystem)DesignPicker.SelectedIndex,
                    (ColorMode)ColorPicker.SelectedIndex,
                    MotionPicker.IsChecked == true
                )
            );
        }
        catch (Exception error)
        {
            ShowAppearance();
            AppearanceStatus.Text = $"Could not change appearance: {error.GetBaseException().Message}";
            LogAppearanceFailure(host.Services.GetRequiredService<ILogger<ShellWindow>>(), error);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not change desktop appearance.")]
    private static partial void LogAppearanceFailure(ILogger logger, Exception exception);
}
