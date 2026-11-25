using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using LazyTuiHub.Desktop;
using Xunit;

namespace LazyTuiHub.Tests;

/// <summary>Drives a page's declared interaction targets and exports their rendered states for local review.</summary>
internal sealed class DesktopVisualScene(DesktopUiDriver driver, string page, DesignSystem design, ColorMode mode)
{
    public const string Trait = "DesktopReview";

    internal sealed record RevealTrace(bool Reduced, double FinalOpacity, IReadOnlyList<double[]> Samples);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Control> controls = [];
    private readonly Dictionary<string, Target> targets = [];
    private readonly Dictionary<string, string> contrast = [];

    private sealed record Target(string Title, Control Control, Control Hover, Control? Alternate, Action? CheckHover)
    {
        public object? Alignment { get; set; }
        public Dictionary<string, string> Samples { get; } = [];
    }

    public static Rect Bounds(Control control, Window window) =>
        new Rect(control.Bounds.Size).TransformToAABB(control.TransformToVisual(window)!.Value);

    public void Track(string key, Control control) => controls.Add(key, control);

    public void Interaction(
        string key,
        string title,
        Control control,
        Control? hover = null,
        Control? alternate = null,
        Action? checkHover = null
    )
    {
        Track(key, control);
        targets.Add(key, new(title, control, hover ?? control, alternate, checkHover));
    }

    public void IconLabel(string key, Control icon, TextBlock label, string? fluentSpecimen = null)
    {
        var iconKey = key + "-icon";
        var labelKey = key + "-label";
        Track(iconKey, icon);
        Track(labelKey, label);
        FontManager.Current.TryGetGlyphTypeface(new Typeface(label.FontFamily, weight: label.FontWeight), out var face);
        targets[key].Alignment = new
        {
            icon = iconKey,
            label = labelKey,
            font = face?.FamilyName,
            size = label.FontSize,
            reference = design == DesignSystem.Fluent ? fluentSpecimen : null,
        };
    }

    public void Contrast(string key, string title, Control control)
    {
        Track(key, control);
        contrast.Add(key, title);
    }

    public async Task CheckAsync(
        Control neutralFocus,
        Func<Task<RevealTrace>>? observeMotion = null,
        Point? restingPointer = null
    )
    {
        var token = TestContext.Current.CancellationToken;
        var window = driver.Window;
        var root = Environment.GetEnvironmentVariable("LAZYTUIHUB_VISUAL_REVIEW");
        var directory =
            root == null ? null : Directory.CreateDirectory(Path.Combine(root, page, $"{design}-{mode}")).FullName;
        Assert.NotEmpty(targets);
        Assert.True(neutralFocus.Focus(NavigationMethod.Tab));
        var original = controls.ToDictionary(e => e.Key, e => Bounds(e.Value, window));
        var rest = restingPointer ?? new Point(window.ClientSize.Width - 2, 2);
        var unsettled = new List<string>();

        async Task Sample(string name, Point at, Control? hovered = null)
        {
            window.MouseMove(at);
            await driver.SettleAsync();
            if (hovered != null)
                Assert.True(hovered.IsPointerOver, $"{page}/{design}/{name}: pointer missed {hovered.Name}.");
            if (directory != null)
            {
                var (pixels, settled) = await driver.CaptureFrameAsync(window);
                if (!settled)
                    unsettled.Add(name);
                await File.WriteAllBytesAsync(Path.Combine(directory, name + ".png"), pixels, token);
            }
            foreach (var (key, control) in controls)
            {
                var actual = Bounds(control, window);
                Assert.True(
                    original[key] == actual,
                    $"{page}/{design}/{name}: {key} moved from {original[key]} to {actual}."
                );
            }
        }

        await Sample("rest", rest);
        Assert.All(targets.Values, target =>
            Assert.False(target.Control.IsPointerOver || target.Control.IsKeyboardFocusWithin)
        );
        if (directory != null)
            await Sample("repeat", rest);
        foreach (var (key, target) in targets)
        {
            Task Take(string state, Point at, string compareTo = "rest", bool hover = false)
            {
                target.Samples.Add(state, compareTo);
                return Sample(key + "-" + state, at, hover ? target.Control : null);
            }
            var point = Bounds(target.Hover, window).Center;
            await Take("hover", point, hover: true);
            target.CheckHover?.Invoke();
            if (target.Alternate != null)
                await Take("alternate", Bounds(target.Alternate, window).Center, hover: true);
            await Take("leave", rest);
            Assert.False(target.Control.IsPointerOver);
            Assert.True(target.Control.Focus(NavigationMethod.Tab));
            await Take("focus", rest);
            Assert.True(target.Control.IsKeyboardFocusWithin);
            await Take("both", point, "focus", hover: true);
            await Take("focus-leave", rest, "focus");
            Assert.True(target.Control.IsKeyboardFocusWithin);
            Assert.True(neutralFocus.Focus(NavigationMethod.Tab));
        }
        var motion = observeMotion == null ? null : await observeMotion();
        if (directory == null)
            return;
        var boxes = original.ToDictionary(
            e => e.Key,
            e => new[] { e.Value.X, e.Value.Y, e.Value.Width, e.Value.Height }
        );
        var components = targets.ToDictionary(
            e => e.Key,
            e => new
            {
                title = e.Value.Title,
                alignment = e.Value.Alignment,
                samples = e.Value.Samples,
            }
        );
        var scene = new
        {
            page,
            design = design.ToString(),
            mode = mode.ToString(),
            boxes,
            unsettled,
            components,
            contrast,
            motion,
            scale = window.RenderScaling,
            renderer = typeof(Window).Assembly.GetName().Version?.ToString(),
            platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        };
        await File.WriteAllTextAsync(
            Path.Combine(directory, "scene.json"),
            JsonSerializer.Serialize(scene, JsonOptions),
            token
        );
    }
}
