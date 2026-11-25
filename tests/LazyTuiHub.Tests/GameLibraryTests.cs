using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Games;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class GameLibraryTests
{
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
