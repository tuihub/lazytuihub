using System.Diagnostics;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using LazyTuiHub.Desktop;
using LazyTuiHub.Games;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed partial class DesktopTests
{
    [Fact]
    [Trait(DesktopVisualScene.Trait, "true")]
    public async Task GameShelfInteractionStates()
    {
        var token = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient(new CoverFixture());
        const string longTitle = "The last lighthouse beyond the northern mountains";
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
                    await ReviewShelfAsync(driver, longTitle, design, mode, token);
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

    private static async Task ReviewShelfAsync(
        DesktopUiDriver driver,
        string longTitle,
        DesignSystem design,
        ColorMode mode,
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
        var monogram = fallback.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Monogram");
        var icon = add.GetVisualDescendants().OfType<PathIcon>().Single();
        var scene = new DesktopVisualScene(driver, "games", design, mode);
        var frame = card.GetVisualDescendants().OfType<Border>().Single(c => c.Name == "CardFrame");
        scene.Interaction("card", "Cover card", card, poster, title);
        scene.Interaction("add", "Add game", add);
        scene.IconLabel("add", icon, label, fluentSpecimen: "add-game");
        scene.Interaction("chip", "Selected filter", chip);
        scene.Track("frame", frame);
        scene.Track("poster", poster);
        scene.Track("title", title);
        scene.Track("fallback", fallback);
        scene.Track("filters", filter);
        scene.Contrast("hint", "Search hint", hint);
        scene.Contrast("monogram", "Fallback monogram", monogram);
        var started = Stopwatch.GetTimestamp();
        while (cover.Source == null && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
            await Task.Delay(16, token);
        Assert.NotNull(cover.Source);
        Assert.True(cover.IsEffectivelyVisible);
        var fallbackImage = fallback.GetVisualDescendants().OfType<Image>().Single();
        Assert.Null(fallbackImage.Source);
        Assert.False(fallbackImage.IsEffectivelyVisible);
        Assert.True(monogram.IsEffectivelyVisible);
        Assert.Equal("T", monogram.Text);
        var captions = new[] { card, fallback }
            .Select(c => c.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("metadata")))
            .ToArray();
        double Baseline(TextBlock text) =>
            DesktopVisualScene.Bounds(text, window).Y + text.TextLayout.TextLines[0].Baseline;
        Assert.Equal(Baseline(captions[0]), Baseline(captions[1]));
        var buttonBounds = DesktopVisualScene.Bounds(add, window);
        Assert.True(buttonBounds.Contains(DesktopVisualScene.Bounds(label, window)));
        Assert.True(buttonBounds.Contains(DesktopVisualScene.Bounds(icon, window)));
        Assert.True(label.TextLayout.Width <= label.Bounds.Width && label.TextLayout.Height <= label.Bounds.Height);
        await scene.CheckAsync(neutral, () => ObserveRevealAsync(driver, card, token));
    }

    private static async Task<DesktopVisualScene.RevealTrace> ObserveRevealAsync(
        DesktopUiDriver driver,
        Button card,
        CancellationToken token
    )
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
            return new(DesktopMotion.Reduced, finalOpacity, samples);
        }
        finally
        {
            panel.PropertyChanged -= Changed;
        }
    }
}
