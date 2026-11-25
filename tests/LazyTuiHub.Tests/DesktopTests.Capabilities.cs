using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using LazyTuiHub.Desktop;
using LazyTuiHub.Games;
using LazyTuiHub.Interaction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed partial class DesktopTests
{
    [Fact]
    public async Task RecordAPlaySessionFromTheDesktopForAGameCreatedOnTheCommandLine()
    {
        var token = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        var game = await workspace.JsonAsync<GameEntry>("games", "add", "--title", "First light");
        await using var session = HeadlessUnitTestSession.StartNew(typeof(DesktopTests));
        await session.Dispatch(
            async () =>
            {
                using var host = CreateHost(workspace);
                var window = new ShellWindow(host);
                window.Show();
                var driver = new DesktopUiDriver(window, token);
                await driver.ClickAsync(window.FindControl<Button>("OperationsButton")!);
                var dialog = Assert.Single(window.OwnedWindows);
                var panel = Assert.IsType<OperationsPage>(dialog.Content);
                await driver.TypeAsync(panel.FindControl<TextBox>("Search")!, "log-session");
                var action = Assert.IsType<Capability>(panel.FindControl<ComboBox>("Choices")!.SelectedItem);
                Assert.Equal("games", action.Group.Name);
                foreach (
                    var (name, value) in new[]
                    {
                        ("game", game.Id),
                        ("device", "Pocket"),
                        ("start", "2026-11-25T12:00:00Z"),
                        ("duration", "00:42:00"),
                    }
                )
                    await driver.TypeAsync(
                        panel
                            .GetVisualDescendants()
                            .OfType<TextBox>()
                            .Single(b => AutomationProperties.GetName(b) == name),
                        value
                    );
                await driver.CaptureAsync("operations-play-session", dialog);
                await driver.ClickAsync(panel.FindControl<Button>("Execute")!);
                var status = panel.FindControl<TextBlock>("Status")!;
                while (status.Text == "Running…")
                    await Task.Delay(10, token);
                Assert.Equal("Completed.", status.Text);
                dialog.Close();
                await CloseAsync(window, token);
                return true;
            },
            token
        );
        using var details = JsonDocument.Parse(await workspace.SuccessAsync("games", "show", "--game", game.Id));
        var recorded = Assert.Single(details.RootElement.GetProperty("sessions").EnumerateArray());
        Assert.Equal("Pocket", recorded.GetProperty("device").GetString());
        Assert.Equal("00:42:00", recorded.GetProperty("duration").GetString());
    }

    [Fact]
    public async Task RemoteDesktopLoadsTheReaderWithoutOpeningALocalDatabase()
    {
        using var workspace = new PluginWorkspace();
        using var host = ApplicationHost.CreateDesktop([
            "--endpoint",
            "https://localhost",
            "--database",
            workspace.Database,
            "--plugin-directory",
            workspace.Plugins,
        ]);
        var token = TestContext.Current.CancellationToken;
        await using var session = HeadlessUnitTestSession.StartNew(typeof(DesktopTests));
        await session.Dispatch(
            async () =>
            {
                var window = new ShellWindow(host, selectedPage: 1);
                window.Show();
                Assert.IsType<OperationsPage>(window.FindControl<ContentControl>("PageContent")!.Content);
                var reader = window.Navigation.Single(p => p.Title == "Reader");
                Assert.IsType<LazyTuiHub.Articles.Desktop.ReaderPage>(reader.Create());
                Assert.DoesNotContain(host.Services.GetRequiredService<CapabilityCatalog>().Groups, g =>
                    g.Name == "games"
                );
                Assert.False(File.Exists(workspace.Database));
                await CloseAsync(window, token);
                return true;
            },
            token
        );
    }
}
