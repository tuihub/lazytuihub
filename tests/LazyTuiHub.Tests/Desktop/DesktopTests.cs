using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Plugin.TuiHub.Articles.Desktop;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk.Avalonia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed partial class DesktopTests
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<DesktopApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    [Fact]
    public async Task ReaderUsesPersistedServicesAndPagesFollowColdPluginAvailability()
    {
        var token = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var fixture = builder.Build();
        var items = string.Concat(
            Enumerable
                .Range(0, 53)
                .Select(i =>
                    $"<item><guid>entry-{i}</guid><title>Article {i} 世界</title><description>Persistent content {i}</description></item>"
                )
        );
        fixture.MapGet("/feed", () =>
            Results.Text(
                $"<rss version=\"2.0\"><channel><title>Desktop fixture</title>{items}</channel></rss>",
                "application/rss+xml"
            )
        );
        await fixture.StartAsync(token);
        try
        {
            await using var ui = HeadlessUnitTestSession.StartNew(typeof(DesktopTests));
            await ui.Dispatch(
                async () =>
                {
                    string savedArticle;
                    using (var host = CreateHost(workspace))
                    {
                        await host.StartAsync(token);
                        Assert.Null(host.Services.GetService<IServer>());
                        var window = new ShellWindow(host);
                        window.Show();
                        var page = window.GetVisualDescendants().OfType<ReaderPage>().Single();
                        await WaitIdleAsync(page.Model, token);
                        Assert.Equal(
                            new[] { "Reader", "Games", "Workflows", "Plugins" },
                            window.Navigation.Select(item => item.Title)
                        );
                        page.Model.FeedUri = fixture.Urls.Single() + "/feed";
                        page.Model.FeedTitle = "Desktop fixture";
                        await page.Model.SubscribeAsync();
                        Assert.Equal("Desktop fixture", page.Model.SelectedFeed!.Title);
                        await page.Model.RefreshAsync();
                        Assert.Equal(50, page.Model.Articles.Count);
                        Assert.True(page.Model.HasMore);
                        await page.Model.MoreAsync();
                        Assert.Equal(53, page.Model.Articles.Count);
                        Assert.False(page.Model.HasMore);
                        savedArticle = page.Model.SelectedArticle!.Name;
                        await page.Model.MarkReadAsync();
                        Assert.True(page.Model.SelectedArticle!.Read);
                        Assert.Contains(
                            "Persistent content",
                            page.FindControl<SelectableTextBlock>("ArticleContent")!.Text
                        );
                        await page.Model.SetFilterAsync("read = false");
                        Assert.DoesNotContain(page.Model.Articles, article => article.Name == savedArticle);
                        await page.Model.SetFilterAsync("read = true");
                        Assert.Equal(savedArticle, Assert.Single(page.Model.Articles).Name);
                        page.Model.FeedUri = "not a feed URL";
                        await page.Model.SubscribeAsync();
                        Assert.Contains("HTTP", page.Model.Status);
                        Assert.False(page.Model.Busy);
                        Assert.Equal(2, page.Model.Feeds.Count);
                        var tabs = window.FindControl<ListBox>("Pages")!;
                        tabs.SelectedIndex = 1;
                        tabs.SelectedIndex = 0;
                        Assert.Same(page, window.GetVisualDescendants().OfType<ReaderPage>().Single());
                        window = await SwitchDesignsAsync(host, window, token);
                        host.Services.GetRequiredService<PluginRuntime>()
                            .SetEnabled("lazytuihub.plugin.tuihub.articles", false);
                        Assert.Equal(
                            new[] { "Reader", "Games", "Workflows", "Plugins" },
                            window.Navigation.Select(item => item.Title)
                        );
                        await CloseAsync(window, token);
                    }
                    using (var disabled = CreateHost(workspace))
                    {
                        Assert.Equal(
                            new(DesignSystem.Material, ColorMode.Dark),
                            disabled.Services.GetRequiredService<DesktopAppearance>().Preferences
                        );
                        var plugins = disabled.Services.GetRequiredService<PluginRuntime>();
                        var record = plugins
                            .List()
                            .Single(plugin => plugin.Saved.Spec.Id == "lazytuihub.plugin.tuihub.articles");
                        Assert.Equal("disabled", record.Activation);
                        Assert.Equal("Reader", Assert.Single(record.Saved.Declaration.Pages).Title);
                        var window = new ShellWindow(disabled);
                        window.Show();
                        Assert.Equal(
                            new[] { "Games", "Workflows", "Plugins" },
                            window.Navigation.Select(page => page.Title)
                        );
                        Assert.Null(disabled.Services.GetService<IArticleApi>());
                        plugins.SetEnabled("lazytuihub.plugin.tuihub.articles", true);
                        await CloseAsync(window, token);
                    }
                    var dll = Path.Combine(
                        workspace.Plugins,
                        "lazytuihub.plugin.tuihub.articles",
                        "LazyTuiHub.Plugin.TuiHub.Articles.dll"
                    );
                    File.Move(dll, dll + ".absent");
                    using (var missing = CreateHost(workspace))
                    {
                        var record = missing
                            .Services.GetRequiredService<PluginRuntime>()
                            .List()
                            .Single(plugin => plugin.Saved.Spec.Id == "lazytuihub.plugin.tuihub.articles");
                        Assert.Equal("missing", record.Availability);
                        Assert.True(record.Saved.Enabled);
                        Assert.Single(record.Saved.Declaration.Pages);
                        Assert.DoesNotContain(new ShellWindow(missing).Navigation, item => item.Title == "Reader");
                    }
                    File.Move(dll + ".absent", dll);
                    using (var restored = CreateHost(workspace))
                    {
                        var page = Assert.IsType<ReaderPage>(
                            DesktopPages.Resolve(restored.Services, "lazytuihub.plugin.tuihub.articles", "reader")
                        );
                        await page.Model.LoadAsync();
                        await page.Model.SetFilterAsync("read = true");
                        Assert.Equal(savedArticle, Assert.Single(page.Model.Articles).Name);
                        restored
                            .Services.GetRequiredService<PluginRuntime>()
                            .SetEnabled("lazytuihub.plugin.tuihub.rss", false);
                    }
                    using (var businessMissing = CreateHost(workspace))
                    {
                        var plugins = businessMissing.Services.GetRequiredService<PluginRuntime>();
                        Assert.Equal(
                            "active",
                            plugins
                                .List()
                                .Single(plugin => plugin.Saved.Spec.Id == "lazytuihub.plugin.tuihub.articles")
                                .Activation
                        );
                        var window = new ShellWindow(businessMissing);
                        window.Show();
                        Assert.Equal(
                            new[] { "Reader", "Games", "Workflows", "Plugins" },
                            window.Navigation.Select(item => item.Title)
                        );
                        var page = window.GetVisualDescendants().OfType<ReaderPage>().Single();
                        await WaitIdleAsync(page.Model, token);
                        Assert.False(page.Model.CanSubscribe);
                        await page.Model.SetFilterAsync("read = true");
                        Assert.Equal(savedArticle, Assert.Single(page.Model.Articles).Name);
                        Assert.NotEmpty(
                            (await plugins.Workflows.DescribeAsync(plugins.Workflows.Get("rss.ingest"), token)).Problems
                        );
                        await CloseAsync(window, token);
                    }
                    return true;
                },
                token
            );
        }
        finally
        {
            await fixture.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void InvalidAppearanceSettingsRemainVisibleAndSaveErrorsAreReported()
    {
        using var workspace = new PluginWorkspace();
        var filename = workspace.Database + ".desktop.json";
        File.WriteAllText(filename, "{ broken json");
        using var host = CreateHost(workspace);
        var appearance = host.Services.GetRequiredService<DesktopAppearance>();
        Assert.Equal(new(), appearance.Preferences);
        Assert.Contains("Could not load", appearance.Status);
        Assert.Equal("{ broken json", File.ReadAllText(filename));
        var settings = new AppearanceSettings(filename);
        settings.Save(new(DesignSystem.Material, ColorMode.Dark));
        Assert.Equal(new(DesignSystem.Material, ColorMode.Dark), settings.Load());
        Assert.Throws<IOException>(() => new AppearanceSettings(workspace.Plugins).Save(new()));
    }

    private static async Task<ShellWindow> SwitchDesignsAsync(IHost host, ShellWindow window, CancellationToken token)
    {
        var initial = window.GetVisualDescendants().OfType<ReaderPage>().Single();
        var model = initial.Model;
        await model.SetFilterAsync("read = false");
        model.SelectedArticle = model.Articles[^1];
        var selected = model.SelectedArticle;
        var filter = model.Filter;
        var count = model.Articles.Count;
        var session = host.Services.GetRequiredService<DesktopSession>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = session.RunAsync(stop => release.Task.WaitAsync(stop));
        model.FeedUri = "https://example.com/draft.xml";
        model.FeedTitle = "Unsubmitted subscription";
        model.AddingSubscription = true;
        foreach (
            var design in new[]
            {
                DesignSystem.Material,
                DesignSystem.MacOS,
                DesignSystem.Fluent,
                DesignSystem.Material,
                DesignSystem.MacOS,
            }
        )
        {
            var previous = window;
            window = window.ChangeAppearance(new(design, ColorMode.Light));
            Assert.NotSame(previous, window);
            Assert.False(previous.IsVisible);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background, token);
            window.UpdateLayout();
            var page = window.GetVisualDescendants().OfType<ReaderPage>().Single();
            Assert.NotSame(initial, page);
            Assert.Same(model, page.Model);
            Assert.Same(selected, model.SelectedArticle);
            Assert.Equal(count, model.Articles.Count);
            Assert.Equal(
                filter,
                Assert.IsType<ComboBoxItem>(page.FindControl<ComboBox>("ReadFilter")!.SelectedItem).Tag
            );
            Assert.Equal(model.FeedUri, page.FindControl<TextBox>("FeedUriInput")!.Text);
            Assert.True(page.Model.AddingSubscription);
            Assert.NotNull(page.FindControl<ComboBox>("FeedPicker")!.Template);
            Assert.False(session.IsStopping);
            Assert.False(pending.IsCompleted);
            var light = Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color;
            Assert.Same(window, window.ChangeAppearance(new(design, ColorMode.Dark)));
            Assert.Equal(ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
            Assert.NotEqual(light, Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);
            Assert.Same(window, window.ChangeAppearance(new(design, ColorMode.System)));
            Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
        }
        release.SetResult();
        await pending;
        var tabs = window.FindControl<ListBox>("Pages")!;
        tabs.SelectedItem = window.Navigation.Single(item => item.Title == "Workflows");
        var workflow = window.GetVisualDescendants().OfType<WorkflowPage>().Single();
        workflow.FindControl<TabControl>("PlanTabs")!.SelectedIndex = 1;
        window = window.ChangeAppearance(new(DesignSystem.Fluent, ColorMode.Light));
        Assert.Equal(
            "Workflows",
            Assert.IsType<LazyTuiHub.Desktop.NavigationPage>(window.FindControl<ListBox>("Pages")!.SelectedItem).Title
        );
        Assert.Equal(
            1,
            window
                .GetVisualDescendants()
                .OfType<WorkflowPage>()
                .Single()
                .FindControl<TabControl>("PlanTabs")!
                .SelectedIndex
        );
        window.FindControl<ListBox>("Pages")!.SelectedItem = window.Navigation.Single(item => item.Title == "Plugins");
        var plugins = window.GetVisualDescendants().OfType<PluginsPage>().Single();
        plugins.FindControl<ListBox>("Plugins")!.SelectedIndex = 1;
        var pluginId = host.Services.GetRequiredService<PluginsPageState>().Selected;
        window = window.ChangeAppearance(new(DesignSystem.Material, ColorMode.Dark));
        Assert.Equal(
            "Plugins",
            Assert.IsType<LazyTuiHub.Desktop.NavigationPage>(window.FindControl<ListBox>("Pages")!.SelectedItem).Title
        );
        Assert.Equal(
            pluginId,
            Assert
                .IsType<PluginView>(
                    window
                        .GetVisualDescendants()
                        .OfType<PluginsPage>()
                        .Single()
                        .FindControl<ListBox>("Plugins")!
                        .SelectedItem
                )
                .Saved.Spec.Id
        );
        return window;
    }

    [Fact]
    public async Task DesktopHostStopsBackgroundTasksWithoutStartingAnHttpServer()
    {
        using var workspace = new PluginWorkspace();
        using var host = CreateHost(workspace, backgroundTasks: true);
        await host.StartAsync(TestContext.Current.CancellationToken);
        Assert.Null(host.Services.GetService<IServer>());
        await host.StopAsync(TestContext.Current.CancellationToken);
        var tasks = await host
            .Services.GetRequiredService<PluginRuntime>()
            .TasksAsync(TestContext.Current.CancellationToken);
        Assert.Contains(tasks, task => task.Declaration.Id == "rss.refresh");
        Assert.All(tasks, task => Assert.Contains(task.State, new[] { "stopped", "completed" }));
    }

    [Fact]
    public async Task ClosingSessionCancelsAndWaitsForItsInFlightOperation()
    {
        using var session = new DesktopSession();
        var cancelled = false;
        var operation = session.RunAsync(async token =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled = true;
            }
        });
        await session.StopAsync();
        Assert.True(cancelled);
        Assert.True(operation.IsCompletedSuccessfully);
    }

    private static IHost CreateHost(PluginWorkspace workspace, bool backgroundTasks = false) =>
        ApplicationHost.CreateDesktop([
            "--database",
            workspace.Database,
            "--desktop-settings",
            workspace.Database + ".desktop.json",
            "--plugin-directory",
            workspace.Plugins,
            "--demo",
            backgroundTasks ? "false" : "true",
            "--Logging:LogLevel:Default",
            "None",
        ]);

    private static async Task WaitIdleAsync(ReaderModel model, CancellationToken token)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background, token);
        while (model.Busy)
            await Task.Delay(10, token);
    }

    private static async Task CloseAsync(Window window, CancellationToken token)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Close();
        await closed.Task.WaitAsync(token);
    }
}
