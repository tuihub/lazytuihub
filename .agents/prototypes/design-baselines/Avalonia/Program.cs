using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DesignProbe;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var design = args[0];
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        await using var session = HeadlessUnitTestSession.StartNew(typeof(Program));
        await session.Dispatch(
            async () =>
            {
                var app = Application.Current!;
                app.Styles.Add(CreateTheme(design));
                foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    app.RequestedThemeVariant = variant;
                    await RenderVariant(design, variant, output);
                }
                await ProbeRetention(design, output);
                return true;
            },
            CancellationToken.None
        );
    }

    private static async Task ProbeRetention(string design, string output)
    {
        var references = Enumerable.Range(0, 4).Select(_ => AttachAndDetach(design)).ToArray();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
#pragma warning disable S1215 // This diagnostic measures reachability after explicit garbage collection.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
#pragma warning restore S1215
        var retained = references.Count(reference => reference.IsAlive);
        await File.WriteAllTextAsync(
            Path.Combine(output, design + "-retention.json"),
            JsonSerializer.Serialize(new { Detached = references.Length, Retained = retained })
        );
        Console.WriteLine(
            $"{design}: {retained}/{references.Length} detached theme objects remain reachable after collection."
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndDetach(string design)
    {
        var theme = CreateTheme(design);
        Application.Current!.Styles.Add(theme);
        Application.Current.Styles.Remove(theme);
        (theme as IDisposable)?.Dispose();
        Devolutions.AvaloniaTheme.MacOS.DevolutionsMacOsTheme.ClearWallpaperTintResources(Application.Current);
        return new WeakReference(theme);
    }

    private const int LastSelectorIndex = 2;
    private static readonly TimeSpan TransitionSettlingTime = TimeSpan.FromMilliseconds(300);
    private static readonly string[] FilterChoices = ["Unread", "All articles", "Saved"];
    private static readonly string[] ArticleChoices =
    [
        "The small details of a good reading experience",
        "Building tools that leave room to think",
    ];

    private sealed record Specimen(
        Window Window,
        Button Primary,
        Button Secondary,
        Button Quiet,
        TextBox Field,
        ComboBox Choices,
        Flyout Popover
    );

    private static Specimen CreateSpecimen(string design, ThemeVariant variant)
    {
        var primary = new Button { Name = "Primary", Content = "Subscribe" };
        var secondary = new Button { Name = "Secondary", Content = "Mark as read" };
        var disabled = new Button
        {
            Name = "Disabled",
            Content = "Unavailable",
            IsEnabled = false,
        };
        var quiet = new Button { Name = "Quiet", Content = "Appearance" };
        var field = new TextBox
        {
            Name = "Input",
            Text = "Design journal",
            Width = 250,
        };
        var choices = new ComboBox
        {
            Name = "Selector",
            ItemsSource = FilterChoices,
            SelectedIndex = 0,
            Width = 250,
        };
        var selection = new ListBox
        {
            Name = "List",
            ItemsSource = ArticleChoices,
            SelectedIndex = 0,
        };
        var popover = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = "Reading preferences", FontSize = 20 },
                    new ToggleSwitch { Content = "Show unread counts", IsChecked = true },
                },
            },
        };
        quiet.Flyout = popover;
        var window = new Window
        {
            Width = 1000,
            Height = 760,
            Content = new StackPanel
            {
                Margin = new Thickness(32),
                Spacing = 24,
                Children =
                {
                    new TextBlock { Text = design + " · " + variant, FontSize = 14 },
                    new TextBlock
                    {
                        Name = "Title",
                        Text = "A quieter place to read",
                        FontSize = 28,
                    },
                    new TextBlock
                    {
                        Name = "Body",
                        Text = "A reader brings subscriptions, articles, and reading preferences together. 阅读札记",
                        FontSize = 16,
                    },
                    selection,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 16,
                        Children = { primary, secondary, disabled, quiet },
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 16,
                        Children = { field, choices },
                    },
                    new CheckBox { Content = "Keep articles offline", IsChecked = true },
                    new ToggleSwitch { Content = "Show unread counts", IsChecked = true },
                },
            },
        };
        window.Show();
        return new(window, primary, secondary, quiet, field, choices, popover);
    }

    private static void ApplyVariants(string design, Specimen specimen)
    {
        var (window, primary, secondary, quiet, _, _, _) = specimen;
        switch (design)
        {
            case "material2":
                secondary.Theme = (ControlTheme)window.FindResource("MaterialOutlineButton")!;
                quiet.Theme = (ControlTheme)window.FindResource("MaterialFlatButton")!;
                break;
            case "material3":
                primary.Classes.Add("Filled");
                secondary.Classes.Add("Outlined");
                quiet.Classes.Add("Text");
                break;
            default:
                primary.Classes.Add("accent");
                if (string.Equals(design, "fluentavalonia", StringComparison.Ordinal))
                    quiet.Theme = (ControlTheme)window.FindResource("TransparentButton")!;
                break;
        }
    }

    private static async Task RenderVariant(string design, ThemeVariant variant, string output)
    {
        var specimen = CreateSpecimen(design, variant);
        var (window, primary, _, quiet, field, choices, popover) = specimen;
        ApplyVariants(design, specimen);
        var stem = Path.Combine(output, design + "-" + variant.ToString().ToLowerInvariant());
        await Settle(window);
        await Capture(window, stem + "-default");
        var center = primary
            .TranslatePoint(new Point(primary.Bounds.Width / 2, primary.Bounds.Height / 2), window)!
            .Value;
        window.MouseMove(center);
        await Capture(window, stem + "-hover");
        primary.Focus(NavigationMethod.Tab);
        await Capture(window, stem + "-focus");
        window.MouseDown(center, MouseButton.Left);
        await Capture(window, stem + "-pressed");
        var clicked = new TaskCompletionSource();
        primary.Click += (_, _) => clicked.TrySetResult();
        window.MouseUp(center, MouseButton.Left);
        if (!clicked.Task.IsCompletedSuccessfully)
            throw new InvalidOperationException("Primary button did not activate.");
        field.Focus();
        field.SelectAll();
        window.KeyTextInput("Typed input 阅读");
        if (!string.Equals(field.Text, "Typed input 阅读", StringComparison.Ordinal))
            throw new InvalidOperationException("Text input failed.");
        var selectorCenter = choices
            .TranslatePoint(new Point(choices.Bounds.Width / 2, choices.Bounds.Height / 2), window)!
            .Value;
        window.MouseDown(selectorCenter, MouseButton.Left);
        window.MouseUp(selectorCenter, MouseButton.Left);
        await Settle(window);
        window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        if (choices.SelectedIndex != LastSelectorIndex)
            throw new InvalidOperationException("Selector keyboard input failed.");
        popover.ShowAt(quiet);
        await Settle(window);
        if (!popover.IsOpen)
            throw new InvalidOperationException("Flyout did not open.");
        await Capture(TopLevel.GetTopLevel((Control)popover.Content!)!, stem + "-popup");
        popover.Hide();
        window.Close();
        Console.WriteLine($"Checked {design} {variant}: button, text input, selector, flyout; captured five states.");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private static Styles CreateTheme(string design) =>
        design switch
        {
            "fluent" => new Avalonia.Themes.Fluent.FluentTheme(),
            "fluentavalonia" => new FluentAvalonia.Styling.FluentAvaloniaTheme { PreferSystemTheme = false },
            "macos" => CreateMacOS(),
            "material2" => CreateMaterial2(),
            "material3" => new CCSWE.Avalonia.Material.MaterialTheme(),
            _ => throw new ArgumentException("Unknown design: " + design, nameof(design)),
        };

    private static Material.Styles.Themes.MaterialTheme CreateMaterial2()
    {
        var theme = new Material.Styles.Themes.MaterialTheme(null!)
        {
            BaseTheme = Material.Styles.Themes.Base.BaseThemeMode.Light,
        };
        theme.BaseTheme = Material.Styles.Themes.Base.BaseThemeMode.Inherit;
        return theme;
    }

    private static Devolutions.AvaloniaTheme.MacOS.DevolutionsMacOsTheme CreateMacOS()
    {
        var theme = new Devolutions.AvaloniaTheme.MacOS.DevolutionsMacOsTheme();
        theme.BeginInit();
        theme.EndInit();
        return theme;
    }

    private static async Task Settle(TopLevel root)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        root.UpdateLayout();
    }

    private static async Task Capture(TopLevel root, string path)
    {
        await Settle(root);
        await Task.Delay(TransitionSettlingTime);
        using var frame = root.CaptureRenderedFrame()!;
        await using (var stream = File.Create(path + ".png"))
            frame.Save(stream, new PngBitmapEncoderOptions());
        var controls = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Name != null)
            .Select(c => new
            {
                c.Name,
                Type = c.GetType().Name,
                Bounds = c.Bounds.ToString(),
                c.IsFocused,
                Font = c switch
                {
                    TextBlock text => DescribeFont(text.FontFamily, text.FontSize, text.FontWeight),
                    Avalonia.Controls.Primitives.TemplatedControl control => DescribeFont(
                        control.FontFamily,
                        control.FontSize,
                        control.FontWeight
                    ),
                    _ => null,
                },
            });
        await File.WriteAllTextAsync(path + ".json", JsonSerializer.Serialize(controls));
    }

    private static object DescribeFont(FontFamily family, double size, FontWeight weight)
    {
        FontManager.Current.TryGetGlyphTypeface(new Typeface(family, weight: weight), out var glyph);
        return new
        {
            RequestedFamily = family.ToString(),
            Size = size,
            Weight = weight.ToString(),
            ResolvedFamily = glyph?.FamilyName,
            ResolvedWeight = glyph?.Weight.ToString(),
        };
    }
}
