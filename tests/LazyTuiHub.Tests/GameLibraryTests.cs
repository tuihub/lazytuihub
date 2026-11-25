using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using LazyTuiHub.Plugin.TuiHub.Games;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class GameLibraryTests
{
    [Fact]
    public async Task SteamStoreUsesItsOwnRobotsGroupAndHonorsRetryAfter()
    {
        using var workspace = new PluginWorkspace();
        using var platform = new GamePlatformFixture();
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http, configure: services =>
            services.AddKeyedSingleton<HttpMessageHandler>("lazytuihub.plugin.tuihub.steam", platform)
        );
        var workflows = app.Services.GetRequiredService<IWorkflowExecutor>();
        var token = TestContext.Current.CancellationToken;
        Task<IReadOnlyList<GameMetadata[]>> Search() =>
            workflows.CollectAsync<GameMetadata[]>("steam.search", new GameRequest("CLANNAD"), token);
        var result = Assert.Single(Assert.Single(await Search()));
        Assert.Equal("324160", result.ExternalId);
        var denied = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
            workflows.CollectAsync<GameMetadata>("steam.lookup", new GameRequest("324160"), token)
        );
        Assert.Contains("robots.txt", denied.Message);
        platform.Interrupt = true;
        var limited = await Assert.ThrowsAsync<PipelineExecutionException>(Search);
        Assert.Contains("Retry-After", limited.Message);
        await Assert.ThrowsAsync<PipelineExecutionException>(Search);
        Assert.Equal(new[] { "/robots.txt", "/api/storesearch/", "/api/storesearch/" }, platform.StorePaths);
    }

    [Fact]
    public async Task LinkAndImportRequireAGameFromTheProvidersWorkflow()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var runtime = app.Services.GetRequiredService<PluginRuntime>();
        runtime.Workflows.Save(new("steam.import", [PipelineNode.Create("query", "games.query")], []));
        var operations = new CapabilityCatalog(runtime.Capabilities);
        var token = TestContext.Current.CancellationToken;
        foreach (var (group, action) in new[] { ("games", "link"), ("steam", "import") })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                operations
                    .Get(group)
                    .Get(action)
                    .InvokeAsync(
                        name => name == "provider" ? "steam" : "42",
                        _ => string.Empty,
                        new(_ => { }, _ => { }),
                        token
                    )
            );
            Assert.Contains("steam.import", error.Message);
            Assert.Contains("GameEntry", error.Message);
        }
    }

    [Fact]
    public async Task SharedListingRefreshesOnlyTheSelectedGameAndSurvivesProviderDisablement()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        var token = TestContext.Current.CancellationToken;
        var snapshot = Path.Combine(workspace.DirectoryPath, "steam.jsonl");
        var listing = new GameMetadata("steam", "42", "Collection", "https://example.org/42");
        Task SaveListing(GameMetadata value) =>
            File.WriteAllTextAsync(snapshot, JsonSerializer.Serialize(value, JsonSerializerOptions.Web), token);
        await SaveListing(listing);
        string first;
        string second;
        await using (
            var app = PluginTestHost.Create(
                workspace.Database,
                http,
                configuration: new() { ["Games:steam:Snapshot"] = snapshot }
            )
        )
        {
            var workflows = app.Services.GetRequiredService<IWorkflowExecutor>();
            Task<IReadOnlyList<GameEntry>> Import(string? target = null) =>
                workflows.CollectAsync<GameEntry>("steam.import", new GameRequest("42", target), token);
            first = Assert.Single(await Import()).Id;
            var library = app.Services.GetRequiredService<GameLibrary>();
            library.Edit(first, "My edition", "Favourites", null);
            second = library.Add("Other edition").Id;
            await Import(second);
            await SaveListing(listing with { Title = "Revised collection" });
            await Assert.ThrowsAsync<PipelineExecutionException>(() => Import());
            await Import(first);
            app.Services.GetRequiredService<PluginRuntime>().SetEnabled("lazytuihub.plugin.tuihub.steam", false);
        }
        await using var restarted = PluginTestHost.Create(workspace.Database, http);
        var games = restarted.Services.GetRequiredService<GameLibrary>();
        var edited = games.Show(first);
        Assert.Equal("My edition", edited.Entry.Title);
        Assert.Equal("Favourites", Assert.Single(edited.Entry.Categories));
        Assert.Equal("Revised collection", Assert.Single(edited.Sources).Title);
        Assert.Equal(listing.Title, Assert.Single(games.Show(second).Sources).Title);
    }
}
