using System.Text.Json;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Devolutions.AvaloniaTheme.MacOS;
using Material.Styles.Themes;
using Material.Styles.Themes.Base;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Desktop;

public sealed partial class DesktopAppearance
{
    private readonly AppearanceSettings settings;
    private bool initialized;
    public AppearancePreferences Preferences { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DesignSystem EffectiveDesign => Resolve(Preferences.Design);

    public DesktopAppearance(AppearanceSettings settings, ILogger<DesktopAppearance> logger)
    {
        this.settings = settings;
        try
        {
            Preferences = settings.Load();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Preferences = new();
            Status = $"Could not load appearance settings: {error.Message}";
            LogSettingsFailure(logger, error);
        }
    }

    public void Initialize(DesktopApp app)
    {
        if (initialized)
            return;
        ReplaceTheme(app, CreateTheme(EffectiveDesign));
        ApplyColor(app);
        initialized = true;
    }

    public bool Change(DesktopApp app, AppearancePreferences preferences)
    {
        var rebuild = Resolve(preferences.Design) != EffectiveDesign;
        settings.Save(preferences);
        if (rebuild)
            ReplaceTheme(app, CreateTheme(Resolve(preferences.Design)));
        Preferences = preferences;
        ApplyColor(app);
        Status = string.Empty;
        return rebuild;
    }

    private static DesignSystem Resolve(DesignSystem design)
    {
        if (design != DesignSystem.Auto)
            return design;
        if (OperatingSystem.IsMacOS())
            return DesignSystem.MacOS;
        return OperatingSystem.IsAndroid() ? DesignSystem.Material : DesignSystem.Fluent;
    }

    private static Styles CreateTheme(DesignSystem design)
    {
        var source = new Uri($"avares://LazyTuiHub.Desktop/Appearance/{design}.axaml");
        var styles = (Styles)new StyleInclude(source) { Source = source }.Loaded;
        // Material needs a concrete initial variant before resource lookup can precede attachment.
        foreach (var material in styles.OfType<MaterialTheme>())
            material.BaseTheme = BaseThemeMode.Inherit;
        return styles;
    }

    private static void ReplaceTheme(DesktopApp app, Styles theme)
    {
        DevolutionsMacOsTheme.ClearWallpaperTintResources(app);
        var previous = app.DesignStyles.ToArray();
        app.DesignStyles.Clear();
        foreach (var style in previous.OfType<Styles>())
        {
            foreach (var disposable in style.OfType<IDisposable>())
                disposable.Dispose();
        }
        app.DesignStyles.Add(theme);
    }

    private void ApplyColor(DesktopApp app) =>
        app.RequestedThemeVariant = Preferences.Color switch
        {
            ColorMode.Light => ThemeVariant.Light,
            ColorMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not load desktop appearance settings.")]
    private static partial void LogSettingsFailure(ILogger logger, Exception exception);
}
