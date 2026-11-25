using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using LazyTuiHub.Articles.Desktop;
using LazyTuiHub.Desktop;
using LazyTuiHub.Games;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed partial class DesktopTests
{
    [Fact]
    [Trait(DesktopVisualScene.Trait, "true")]
    public async Task ReaderCanBeDrivenWithPointerAndKeyboard()
    {
        var token = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var feed = builder.Build();
        feed.MapGet("/feed.xml", () => Results.Text(LayoutFeed, "application/rss+xml"));
        await feed.StartAsync(token);
        try
        {
            await using var session = HeadlessUnitTestSession.StartNew(typeof(DesktopTests));
            await session.Dispatch(
                async () =>
                {
                    using var host = CreateHost(workspace);
                    await host.StartAsync(token);
                    var window = new ShellWindow(host);
                    window.Show();
                    var driver = new DesktopUiDriver(window, token);
                    var reader = window.GetVisualDescendants().OfType<ReaderPage>().Single();
                    await WaitIdleAsync(reader.Model, token);
                    await driver.SettleAsync();
                    await driver.CaptureAsync("empty-library");
                    await driver.ClickAsync(reader.FindControl<Button>("AddFeedButton")!);
                    await driver.TypeAsync(
                        reader.FindControl<TextBox>("FeedUriInput")!,
                        feed.Urls.Single() + "/feed.xml"
                    );
                    await driver.TypeAsync(reader.FindControl<TextBox>("FeedTitleInput")!, "Field Notes · 阅读札记");
                    await driver.CaptureAsync("subscription");
                    await driver.ClickAsync(reader.FindControl<Button>("SubscribeButton")!);
                    await WaitIdleAsync(reader.Model, token);
                    Assert.False(reader.Model.AddingSubscription);
                    await driver.ClickAsync(reader.FindControl<Button>("RefreshButton")!);
                    await WaitIdleAsync(reader.Model, token);
                    Assert.Equal(3, reader.Model.Articles.Count);
                    foreach (var design in new[] { DesignSystem.Fluent, DesignSystem.MacOS, DesignSystem.Material })
                    {
                        window = window.ChangeAppearance(new(design, ColorMode.Light));
                        driver.Window = window;
                        await driver.SettleAsync();
                        reader = window.GetVisualDescendants().OfType<ReaderPage>().Single();
                        await ReviewReaderToolbarAsync(driver, reader, design, ColorMode.Light);
                        await ExerciseActionsAsync(driver, reader, design);
                        await ExerciseResponsiveReaderAsync(driver, reader, design.ToString());
                        await ExerciseAppearanceAsync(driver, design.ToString());
                        await ReviewReaderToolbarAsync(driver, reader, design, ColorMode.Dark);
                    }
                    var library = host.Services.GetRequiredService<GameLibrary>();
                    using (var fixture = new GamePlatformFixture())
                    using (var http = new HttpClient(fixture))
                    await using (var seeded = PluginTestHost.Create(workspace.Database, http))
                    {
                        var connections = seeded.Services.GetRequiredService<GameConnections>();
                        var folder = Directory.CreateDirectory(
                            Path.Combine(workspace.DirectoryPath, "Library", "CLANNAD")
                        );
                        await connections.ScanAsync("folder", folder.Parent!.FullName, token);
                        var game = Assert.Single(library.Shelf());
                        library.Import(
                            new(
                                "vndb",
                                "v4",
                                "CLANNAD",
                                "https://vndb.org/v4",
                                "[url=https://vndb.org]A story[/url]",
                                AlternativeTitle: "クラナド",
                                Developers: ["Key"]
                            ),
                            game.Id
                        );
                        library.Import(
                            new("bangumi", "13", "CLANNAD", "https://bgm.tv/subject/13", ReleaseDate: "2004-04-28"),
                            game.Id
                        );
                        var account = await connections.BindAsync(
                            "bangumi",
                            "bgm-test",
                            _ => throw new NotSupportedException(),
                            token
                        );
                        await connections.SyncAsync(account.Id, token);
                    }
                    library.Add("Untitled prototype");
                    await ExerciseGameLibraryAsync(driver, library);
                    await ExerciseInspectorsAsync(driver);
                    await CloseAsync(driver.Window, token);
                    return true;
                },
                token
            );
        }
        finally
        {
            await feed.StopAsync(CancellationToken.None);
        }
    }

    private static Task ReviewReaderToolbarAsync(
        DesktopUiDriver driver,
        ReaderPage reader,
        DesignSystem design,
        ColorMode mode
    )
    {
        var scene = new DesktopVisualScene(driver, "reader", design, mode);
        scene.Interaction("subscribe", "Add subscription", reader.FindControl<Button>("AddFeedButton")!);
        scene.Track("feed-picker", reader.FindControl<ComboBox>("FeedPicker")!);
        return scene.CheckAsync(reader.FindControl<ComboBox>("ReadFilter")!);
    }

    private static async Task ExerciseResponsiveReaderAsync(DesktopUiDriver driver, ReaderPage reader, string design)
    {
        var list = reader.FindControl<ListBox>("ArticleList")!;
        var pane = reader.FindControl<AdaptivePane>("ReaderPane")!;
        await driver.ResizeAsync(1280, 800);
        await driver.ClickAsync(list.ContainerFromIndex(2)!);
        await driver.CaptureAsync(design + "-wide");
        await driver.ResizeAsync(820, 740);
        await driver.CaptureAsync(design + "-medium");
        await driver.ResizeAsync(390, 740);
        Assert.True(pane.DetailOpen);
        await driver.CaptureAsync(design + "-narrow-article");
        await driver.ClickAsync(pane.FindControl<Button>("Back")!);
        Assert.False(pane.DetailOpen);
        await driver.CaptureAsync(design + "-narrow-list");
        await driver.ClickAsync(list.ContainerFromIndex(1)!);
        Assert.True(pane.DetailOpen);
        Assert.Same(list.SelectedItem, reader.Model.SelectedArticle);
        if (!reader.Model.SelectedArticle!.Read)
        {
            await driver.ClickAsync(reader.FindControl<Button>("MarkReadButton")!);
            await WaitIdleAsync(reader.Model, TestContext.Current.CancellationToken);
        }
        Assert.True(reader.Model.SelectedArticle!.Read);
        var body = reader.FindControl<SelectableTextBlock>("ArticleContent")!;
        var right = body.TranslatePoint(new(body.Bounds.Width, 0), driver.Window)!.Value.X;
        Assert.InRange(right, 1, driver.Window.ClientSize.Width);
        await driver.ClickAsync(pane.FindControl<Button>("Back")!);
        var selectedItem = list.ContainerFromIndex(1)!;
        Assert.False(pane.DetailOpen);
        Assert.True(
            selectedItem.IsFocused,
            $"Focusable={selectedItem.Focusable}, enabled={selectedItem.IsEffectivelyEnabled}, visible={selectedItem.IsEffectivelyVisible}, focus={driver.Window.FocusManager?.GetFocusedElement()}, selected={list.SelectedIndex}"
        );
        await driver.KeyAsync(driver.Window, Key.Enter);
        Assert.True(pane.DetailOpen);
        await driver.ResizeAsync(1280, 800);
    }

    private static async Task ExerciseActionsAsync(DesktopUiDriver driver, ReaderPage reader, DesignSystem design)
    {
        var add = reader.FindControl<Button>("AddFeedButton")!;
        var glyph = Assert.IsType<PathIcon>(add.Content);
        Assert.NotNull(glyph.Data);
        Assert.True(glyph.Data.Bounds.Width > 0 && glyph.Data.Bounds.Height > 0);
        Assert.Equal("Add subscription", AutomationProperties.GetName(add));
        if (design == DesignSystem.Material)
        {
            var edge = add.TranslatePoint(new Point(1, 1), driver.Window)!.Value;
            driver.Window.MouseMove(edge);
            driver.Window.MouseDown(edge, MouseButton.Left);
            driver.Window.MouseUp(edge, MouseButton.Left);
            await driver.SettleAsync();
        }
        else
            await driver.ClickAsync(add);
        Assert.True(reader.Model.AddingSubscription, $"{design}: open subscription panel");
        var submit = reader.FindControl<Button>("SubscribeButton")!;
        Assert.True(reader.FindControl<TextBox>("FeedUriInput")!.IsFocused);
        await driver.ClickAsync(reader.FindControl<TextBox>("FeedTitleInput")!);
        await driver.KeyAsync(driver.Window, Key.Tab);
        Assert.True(submit.IsFocused, $"{design}: Tab from the name field focuses Subscribe.");
        await driver.CaptureAsync(design + "-action-focus");
        if (design == DesignSystem.Material)
        {
            var ink = Assert.Single(submit.GetVisualDescendants().OfType<MaterialInk>());
            Assert.True(submit.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "FocusRing").IsVisible);
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(submit.FontFamily), out var face));
            Assert.Equal("Roboto", face.FamilyName);
            var point = submit.TranslatePoint(new Point(10, 10), driver.Window)!.Value;
            driver.Window.MouseDown(point, MouseButton.Left);
            await driver.SettleAsync();
            Assert.True(ink.Pressed);
            await driver.CaptureAsync("Material-action-pressed");
            driver.Window.MouseMove(default);
            driver.Window.MouseUp(default, MouseButton.Left);
            Assert.False(ink.Pressed);
        }
        var close = reader.FindControl<Button>("CloseSubscriptionButton")!;
        close.Focus(NavigationMethod.Tab);
        await driver.KeyAsync(driver.Window, Key.Space);
        Assert.False(reader.Model.AddingSubscription);
        Assert.True(reader.FindControl<Button>("AddFeedButton")!.IsFocused);
    }

    private static async Task ExerciseAppearanceAsync(DesktopUiDriver driver, string design)
    {
        var button = driver.Window.FindControl<Button>("AppearanceButton")!;
        var flyout = Assert.IsType<Flyout>(button.Flyout);
        await driver.ClickAsync(button);
        Assert.True(flyout.IsOpen);
        var content = Assert.IsAssignableFrom<Control>(flyout.Content);
        Assert.True(content.IsEffectivelyVisible);
        var picker = content.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "ColorPicker");
        await driver.ClickAsync(picker);
        var popupRoot = TopLevel.GetTopLevel(picker)!;
        await driver.KeyAsync(popupRoot, Key.End);
        await driver.KeyAsync(popupRoot, Key.Enter);
        Assert.Equal((int)ColorMode.Dark, picker.SelectedIndex);
        await driver.CaptureAsync(design + "-appearance", popupRoot);
        flyout.Hide();
        await driver.SettleAsync();
        await driver.CaptureAsync(design + "-dark");
    }

    private static async Task ExerciseInspectorsAsync(DesktopUiDriver driver)
    {
        await driver.ResizeAsync(390, 740);
        var pages = driver.Window.FindControl<ListBox>("Pages")!;
        await driver.ClickAsync(
            pages.ContainerFromItem(
                pages.Items.OfType<LazyTuiHub.Desktop.NavigationPage>().Single(page => page.Title == "Workflows")
            )!
        );
        var workflow = driver.Window.GetVisualDescendants().OfType<WorkflowPage>().Single();
        var definitions = workflow.FindControl<ListBox>("Definitions")!;
        await driver.ClickAsync(definitions.ContainerFromIndex(0)!);
        Assert.True(workflow.FindControl<AdaptivePane>("Pane")!.DetailOpen);
        await driver.CaptureAsync("workflow-narrow");
        await driver.ClickAsync(
            pages.ContainerFromItem(
                pages.Items.OfType<LazyTuiHub.Desktop.NavigationPage>().Single(page => page.Title == "Plugins")
            )!
        );
        var plugins = driver.Window.GetVisualDescendants().OfType<PluginsPage>().Single();
        await driver.ClickAsync(plugins.FindControl<ListBox>("Plugins")!.ContainerFromIndex(0)!);
        Assert.True(plugins.FindControl<AdaptivePane>("Pane")!.DetailOpen);
        await driver.CaptureAsync("plugins-narrow");
    }

    private const string LayoutFeed = """
        <rss version="2.0"><channel><title>Field Notes</title><link>https://example.com</link><description>Reading fixture</description>
          <item><guid>city</guid><title>在城市里，留一点时间给阅读</title><link>https://example.com/reading-in-the-city</link>
            <pubDate>Tue, 17 Nov 2026 09:00:00 GMT</pubDate><description>清晨，街角的咖啡馆还没有坐满。我们翻开收藏的文章，读到一个让人停下来的句子。

        阅读器把来自不同地方的文字放在一起。列表帮助我们找到下一篇，正文让内容有地方舒展。

        A good reading space lets the words breathe. The layout adapts to the window, while your place in the library stays with you.

        A narrow screen invites one task at a time. A wide screen lets a list and a document sit together. Both views should feel like the same place.</description></item>
          <item><guid>spaces</guid><title>Small tools, thoughtful spaces</title><link>https://example.com/thoughtful-spaces</link>
            <pubDate>Mon, 16 Nov 2026 10:00:00 GMT</pubDate><description>A personal workspace grows around the things you want to keep. Small, thoughtful choices make familiar tasks easier.</description></item>
          <item><guid>long</guid><title>A longer article title that wraps naturally when the reading list is narrow</title>
            <link>https://example.com/long-title</link><pubDate>Sun, 15 Nov 2026 10:00:00 GMT</pubDate>
            <description>This entry checks that longer titles remain readable without expanding the page beyond the available window width.</description></item>
        </channel></rss>
        """;
}
