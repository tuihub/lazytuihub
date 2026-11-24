using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using LazyTuiHub.Desktop;
using LazyTuiHub.Games;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed partial class DesktopTests
{
    [Fact]
    public async Task GameShelfInteractionStates()
    {
        var token = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient(new CoverFixture());
        const string longTitle = "The last lighthouse beyond the northern mountains";
        var directory = Environment.GetEnvironmentVariable("LAZYTUIHUB_VISUAL_REVIEW");
        var settings = new AppearanceSettings(workspace.Database + ".desktop.json");
        await using var session = HeadlessUnitTestSession.StartNew(typeof(DesktopTests));
        await session.Dispatch(
            async () =>
            {
                foreach (var design in new[] { DesignSystem.Fluent, DesignSystem.MacOS, DesignSystem.Material })
                foreach (var mode in new[] { ColorMode.Light, ColorMode.Dark })
                {
                    settings.Save(new(design, mode, ReduceMotion: mode == ColorMode.Dark));
                    await using var host = PluginTestHost.Create(
                        workspace.Database,
                        http,
                        workspace.Plugins,
                        configure: services =>
                        {
                            services.AddSingleton<DesktopSession>();
                            services.AddSingleton(settings);
                            services.AddSingleton<DesktopAppearance>();
                        }
                    );
                    var library = host.Services.GetRequiredService<GameLibrary>();
                    if (library.Shelf().Count == 0)
                    {
                        library.Import(
                            new(
                                "vndb",
                                "v4",
                                "CLANNAD",
                                "https://vndb.org/v4",
                                ImageUrl: CoverFixture.Url,
                                Developers: ["Key"]
                            )
                        );
                        library.Import(
                            new(
                                "fixture",
                                "second",
                                longTitle,
                                "https://example.invalid/second",
                                Developers: ["Studio"]
                            )
                        );
                    }
                    var window = new ShellWindow(host) { Width = 1280, Height = 800 };
                    window.Show();
                    var driver = new DesktopUiDriver(window, token);
                    window.FindControl<ListBox>("Pages")!.SelectedItem = window.Navigation.Single(p =>
                        p.Title == "Games"
                    );
                    await driver.SettleAsync();
                    var path =
                        directory == null
                            ? null
                            : Directory.CreateDirectory(Path.Combine(directory, $"{design}-{mode}")).FullName;
                    await ReviewShelfAsync(driver, longTitle, path, token);
                    await CloseAsync(window, token);
                }
                return true;
            },
            token
        );
    }

    private sealed class CoverFixture : HttpMessageHandler
    {
        public const string Url = "https://covers.example.invalid/fixture.png";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Assert.Equal(Url, request.RequestUri?.AbsoluteUri);
            var stream = typeof(DesktopTests).Assembly.GetManifestResourceStream("review-cover.png")!;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        }
    }

    private static Rect OnWindow(Control control, Window window) =>
        new Rect(control.Bounds.Size).TransformToAABB(control.TransformToVisual(window)!.Value);

    private static async Task ReviewShelfAsync(
        DesktopUiDriver driver,
        string longTitle,
        string? directory,
        CancellationToken token
    )
    {
        var window = driver.Window;
        var card = GameCard(driver, "CLANNAD");
        Assert.DoesNotContain("chosen", card.Classes);
        var fallback = GameCard(driver, longTitle);
        var poster = card.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "Poster");
        var cover = poster.GetVisualDescendants().OfType<Image>().Single();
        var neutral = GameControl<Button>(driver, "SourceButton");
        var add = GameControl<Button>(driver, "AddButton");
        var filter = GameControl<ListBox>(driver, "States");
        var chip = filter.ContainerFromIndex(0)!;
        var hint = GameControl<TextBlock>(driver, "SearchHint");
        var label = add.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Add game");
        var title = card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "CLANNAD");
        var controls = new Dictionary<string, Control>
        {
            ["card"] = card,
            ["frame"] = card.GetVisualDescendants().OfType<Border>().Single(c => c.Name == "CardFrame"),
            ["poster"] = poster,
            ["title"] = title,
            ["fallback"] = fallback,
            ["monogram"] = fallback.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Monogram"),
            ["add"] = add,
            ["icon"] = add.GetVisualDescendants().OfType<PathIcon>().Single(),
            ["label"] = label,
            ["chip"] = chip,
            ["filters"] = filter,
            ["hint"] = hint,
        };
        var started = Stopwatch.GetTimestamp();
        while (cover.Source == null && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
            await Task.Delay(16, token);
        Assert.NotNull(cover.Source);
        Assert.True(cover.IsEffectivelyVisible);
        var fallbackImage = fallback.GetVisualDescendants().OfType<Image>().Single();
        Assert.Null(fallbackImage.Source);
        Assert.False(fallbackImage.IsEffectivelyVisible);
        var monogram = Assert.IsType<TextBlock>(controls["monogram"]);
        Assert.True(monogram.IsEffectivelyVisible);
        Assert.Equal("T", monogram.Text);
        var captions = new[] { card, fallback }
            .Select(c => c.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("metadata")))
            .ToArray();
        double Baseline(TextBlock text) => OnWindow(text, window).Y + text.TextLayout.TextLines[0].Baseline;
        Assert.Equal(Baseline(captions[0]), Baseline(captions[1]));
        var original = controls.ToDictionary(entry => entry.Key, entry => OnWindow(entry.Value, window));
        Assert.True(original["add"].Contains(original["label"]));
        Assert.True(original["add"].Contains(original["icon"]));
        Assert.True(label.TextLayout.Width <= label.Bounds.Width && label.TextLayout.Height <= label.Bounds.Height);
        var rest = new Point(window.ClientSize.Width - 2, 2);
        Assert.True(neutral.Focus(NavigationMethod.Tab));
        var states = new List<string>();
        var unsettled = new List<string>();
        async Task Sample(string name, Point at, Control? hovered = null)
        {
            window.MouseMove(at);
            await driver.SettleAsync();
            if (hovered != null)
                Assert.True(hovered.IsPointerOver, $"{name}: pointer did not reach {hovered.Name}.");
            if (directory != null)
            {
                states.Add(name);
                var (pixels, settled) = await driver.CaptureFrameAsync(window);
                if (!settled)
                    unsettled.Add(name);
                await File.WriteAllBytesAsync(Path.Combine(directory, name + ".png"), pixels, token);
            }
            foreach (var (key, control) in controls)
            {
                var actual = OnWindow(control, window);
                Assert.True(original[key] == actual, $"{name}: {key} moved from {original[key]} to {actual}.");
            }
        }
        await Sample("rest", rest);
        Assert.False(card.IsPointerOver || add.IsPointerOver || chip.IsPointerOver);
        await Sample("repeat", rest);
        foreach (var name in new[] { "card", "add", "chip" })
        {
            var control = controls[name];
            var point = original[name == "card" ? "poster" : name].Center;
            await Sample(name + "-hover", point, control);
            if (name == "card")
                await Sample("card-title", original["title"].Center, card);
            await Sample(name + "-leave", rest);
            Assert.False(control.IsPointerOver);
            Assert.True(control.Focus(NavigationMethod.Tab));
            await Sample(name + "-focus", rest);
            Assert.True(control.IsKeyboardFocusWithin);
            await Sample(name + "-both", point, control);
            await Sample(name + "-focus-leave", rest);
            Assert.True(control.IsKeyboardFocusWithin);
            Assert.True(neutral.Focus(NavigationMethod.Tab));
        }
        var motion = await ObserveRevealAsync(driver, card, token);
        if (directory == null)
            return;
        var boxes = original.ToDictionary(
            e => e.Key,
            e => new[] { e.Value.X, e.Value.Y, e.Value.Width, e.Value.Height }
        );
        FontManager.Current.TryGetGlyphTypeface(new Typeface(label.FontFamily, weight: label.FontWeight), out var face);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "scene.json"),
            JsonSerializer.Serialize(
                new
                {
                    boxes,
                    states,
                    unsettled,
                    scale = window.RenderScaling,
                    font = face?.FamilyName,
                    label.FontSize,
                    renderer = typeof(Window).Assembly.GetName().Version?.ToString(),
                    platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    motion,
                }
            ),
            token
        );
    }

    private static async Task<object> ObserveRevealAsync(DesktopUiDriver driver, Button card, CancellationToken token)
    {
        var panel = driver.Window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "InspectHost");
        var samples = new List<double[]>();
        var start = Stopwatch.GetTimestamp();
        void Changed(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == Visual.OpacityProperty)
                samples.Add([Stopwatch.GetElapsedTime(start).TotalMilliseconds, panel.Opacity]);
        }
        panel.PropertyChanged += Changed;
        try
        {
            await driver.ClickAsync(card);
            while (panel.Opacity < 1 && Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(16, token);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Assert.True(panel.IsEffectivelyVisible);
            Assert.Equal(1, panel.Opacity);
            if (DesktopMotion.Reduced)
                Assert.DoesNotContain(samples, sample => sample[1] < 1);
            else
                Assert.Contains(samples, sample => sample[1] == 0);
            var finalOpacity = panel.Opacity;
            await driver.ClickAsync(GameControl<Button>(driver, "CloseDetail"));
            return new
            {
                reduced = DesktopMotion.Reduced,
                finalOpacity,
                samples,
            };
        }
        finally
        {
            panel.PropertyChanged -= Changed;
        }
    }
}
