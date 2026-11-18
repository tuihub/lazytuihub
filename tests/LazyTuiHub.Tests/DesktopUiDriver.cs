using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace LazyTuiHub.Tests;

internal sealed class DesktopUiDriver(Window window, CancellationToken token)
{
    public Window Window { get; set; } = window;

    public async Task ResizeAsync(double width, double height)
    {
        Window.Width = width;
        Window.Height = height;
        await SettleAsync();
    }

    public async Task ClickAsync(Control target)
    {
        var root = TopLevel.GetTopLevel(target)!;
        var started = Stopwatch.GetTimestamp();
        Point center;
        while (true)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            root.UpdateLayout();
            center = target.TranslatePoint(new(target.Bounds.Width / 2, target.Bounds.Height / 2), root)!.Value;
            var hit = root.InputHitTest(center) as Visual;
            if (
                target.IsEffectivelyEnabled
                && target.IsEffectivelyVisible
                && hit?.GetSelfAndVisualAncestors().Contains(target) == true
            )
                break;
            Assert.True(
                Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5),
                $"{target.Name} never became hittable at {center}."
            );
            await Task.Delay(16, token);
        }
        root.MouseMove(center);
        root.MouseDown(center, MouseButton.Left);
        root.MouseUp(center, MouseButton.Left);
        await SettleAsync();
    }

    public async Task TypeAsync(TextBox target, string text)
    {
        await ClickAsync(target);
        target.SelectAll();
        TopLevel.GetTopLevel(target)!.KeyTextInput(text);
        await SettleAsync();
    }

    public async Task KeyAsync(TopLevel root, Key key)
    {
        root.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        root.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        await SettleAsync();
    }

    public async Task SettleAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background, token);
        Window.UpdateLayout();
    }

    public async Task CaptureAsync(string name, TopLevel? root = null)
    {
        var directory = Environment.GetEnvironmentVariable("LAZYTUIHUB_UI_ARTIFACTS");
        if (string.IsNullOrEmpty(directory))
            return;
        Directory.CreateDirectory(directory);
        byte[] previous = [];
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            using var frame = (root ?? Window).CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var image = new MemoryStream();
            frame.Save(image, new PngBitmapEncoderOptions());
            var current = image.ToArray();
            var settled = current.AsSpan().SequenceEqual(previous);
            previous = current;
            if (settled || Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(5))
                break;
            await Task.Delay(16, token);
        }
        await File.WriteAllBytesAsync(Path.Combine(directory, name + ".png"), previous, token);
        var visualRoot = root ?? Window;
        var controls = visualRoot
            .GetVisualDescendants()
            .OfType<Control>()
            .Where(control =>
                control.IsEffectivelyVisible && (control.Name != null || control is TextBlock or Button or ComboBox)
            )
            .Select(control => new
            {
                Type = control.GetType().Name,
                control.Name,
                Text = control is TextBlock text ? text.Text : null,
                Position = control.TranslatePoint(default, visualRoot)?.ToString(),
                Size = control.Bounds.Size.ToString(),
                Enabled = control.IsEffectivelyEnabled,
                Focused = control.IsFocused,
                Typography = DescribeTypography(control),
            });
        await File.WriteAllTextAsync(
            Path.Combine(directory, name + ".json"),
            JsonSerializer.Serialize(controls),
            token
        );
    }

    private static object? DescribeTypography(Control control) =>
        control switch
        {
            TextBlock text => DescribeFont(text.FontFamily, text.FontSize, text.FontWeight, text.Foreground),
            TemplatedControl item => DescribeFont(item.FontFamily, item.FontSize, item.FontWeight, item.Foreground),
            _ => null,
        };

    private static object DescribeFont(FontFamily family, double fontSize, FontWeight weight, IBrush? foreground)
    {
        FontManager.Current.TryGetGlyphTypeface(new Typeface(family, weight: weight), out var face);
        return new
        {
            Family = family.ToString(),
            ResolvedFamily = face?.FamilyName,
            FontSize = fontSize,
            Weight = weight.ToString(),
            Foreground = foreground?.ToString(),
        };
    }
}
