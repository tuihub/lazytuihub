using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using LazyTuiHub.Api;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using LazyTuiHub.Rss.Desktop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class DesktopTests
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<DesktopApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

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
                        var page = Assert.IsType<ReaderPage>(
                            DesktopPages.Resolve(host.Services, "rss.desktop", "reader")
                        );
                        await WaitIdleAsync(page.Model, token);
                        Assert.Equal(3, window.Navigation.Count);
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
                        Assert.Contains("Persistent content", page.FindControl<TextBox>("ArticleContent")!.Text);
                        await page.Model.SetFilterAsync("read = false");
                        Assert.DoesNotContain(page.Model.Articles, article => article.Name == savedArticle);
                        await page.Model.SetFilterAsync("read = true");
                        Assert.Equal(savedArticle, Assert.Single(page.Model.Articles).Name);
                        page.Model.FeedUri = "not a feed URL";
                        await page.Model.SubscribeAsync();
                        Assert.Contains("HTTP", page.Model.Status);
                        Assert.False(page.Model.Busy);
                        Assert.Equal(2, page.Model.Feeds.Count);
                        var tabs = window.FindControl<TabControl>("Pages")!;
                        tabs.SelectedIndex = 1;
                        tabs.SelectedIndex = 0;
                        Assert.Same(page, DesktopPages.Resolve(host.Services, "rss.desktop", "reader"));
                        host.Services.GetRequiredService<PluginRuntime>().SetEnabled("rss.desktop", false);
                        Assert.Equal(3, window.Navigation.Count);
                        await CloseAsync(window, token);
                    }
                    using (var disabled = CreateHost(workspace))
                    {
                        var plugins = disabled.Services.GetRequiredService<PluginRuntime>();
                        var record = plugins.List().Single(plugin => plugin.Saved.Spec.Id == "rss.desktop");
                        Assert.Equal("disabled", record.Activation);
                        Assert.Equal("Reader", Assert.Single(record.Saved.Declaration.Pages).Title);
                        var window = new ShellWindow(disabled);
                        window.Show();
                        Assert.Equal(new[] { "Workflows", "Plugins" }, window.Navigation.Select(page => page.Title));
                        Assert.True(
                            (
                                await disabled
                                    .Services.GetRequiredService<IRssApi>()
                                    .GetArticleAsync(new() { Name = savedArticle }, token)
                            ).Read
                        );
                        plugins.SetEnabled("rss.desktop", true);
                        await CloseAsync(window, token);
                    }
                    var dll = Path.Combine(workspace.Plugins, "rss-desktop", "LazyTuiHub.Rss.Desktop.dll");
                    File.Move(dll, dll + ".absent");
                    using (var missing = CreateHost(workspace))
                    {
                        var record = missing
                            .Services.GetRequiredService<PluginRuntime>()
                            .List()
                            .Single(plugin => plugin.Saved.Spec.Id == "rss.desktop");
                        Assert.Equal("missing", record.Availability);
                        Assert.True(record.Saved.Enabled);
                        Assert.Single(record.Saved.Declaration.Pages);
                        Assert.Equal(2, new ShellWindow(missing).Navigation.Count);
                    }
                    File.Move(dll + ".absent", dll);
                    using (var restored = CreateHost(workspace))
                    {
                        var page = Assert.IsType<ReaderPage>(
                            DesktopPages.Resolve(restored.Services, "rss.desktop", "reader")
                        );
                        await page.Model.LoadAsync();
                        await page.Model.SetFilterAsync("read = true");
                        Assert.Equal(savedArticle, Assert.Single(page.Model.Articles).Name);
                        restored.Services.GetRequiredService<PluginRuntime>().SetEnabled("rss", false);
                    }
                    using (var businessMissing = CreateHost(workspace))
                    {
                        var plugins = businessMissing.Services.GetRequiredService<PluginRuntime>();
                        Assert.Equal(
                            "blocked",
                            plugins.List().Single(plugin => plugin.Saved.Spec.Id == "rss.desktop").Activation
                        );
                        var window = new ShellWindow(businessMissing);
                        window.Show();
                        Assert.Equal(2, window.Navigation.Count);
                        Assert.NotEmpty(plugins.Workflows.Describe(plugins.Workflows.Get("rss.ingest")).Problems);
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
    public async Task DesktopHostStopsBackgroundTasksWithoutStartingAnHttpServer()
    {
        using var workspace = new PluginWorkspace();
        using var host = CreateHost(workspace, backgroundTasks: true);
        await host.StartAsync(TestContext.Current.CancellationToken);
        Assert.Null(host.Services.GetService<IServer>());
        await host.StopAsync(TestContext.Current.CancellationToken);
        var tasks = host.Services.GetRequiredService<PluginRuntime>().Tasks();
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
            "--plugin-directory",
            workspace.Plugins,
            "--demo",
            backgroundTasks ? "false" : "true",
            "--Logging:LogLevel:Default",
            "None",
        ]);

    private static async Task WaitIdleAsync(ReaderModel model, CancellationToken token)
    {
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
