using LazyTuiHub.Framework;
using LazyTuiHub.Games;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class GameInventoryTests
{
    [Fact]
    public async Task AccountsAndInstallationShareAWorkWithoutLosingEvidenceOnFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        var dataset = Path.Combine(workspace.DirectoryPath, "ids.json");
        await File.WriteAllTextAsync(dataset, """{"entries":[{"steam":[324160],"bangumi":[13],"vndb":["v4"]}]}""", ct);
        using var platform = new GamePlatformFixture();
        using var http = new HttpClient(platform);
        await using var app = PluginTestHost.Create(
            workspace.Database,
            http,
            configuration: new() { ["Games:IdDataset"] = dataset }
        );
        var games = app.Services.GetRequiredService<GameLibrary>();
        var connections = app.Services.GetRequiredService<GameConnections>();
        var bgm = await connections.BindAsync("bangumi", "bgm-test", _ => throw new NotSupportedException(), ct);
        var vn = await connections.BindAsync("vndb", "vn-test", _ => throw new NotSupportedException(), ct);
        Assert.Equal("42", bgm.ExternalId);
        Assert.Equal("u123", vn.ExternalId);
        await connections.SyncAsync(bgm.Id, ct);
        await connections.SyncAsync(vn.Id, ct);
        var apps = Path.Combine(workspace.DirectoryPath, "steamapps");
        Directory.CreateDirectory(Path.Combine(apps, "common", "CLANNAD"));
        await File.WriteAllTextAsync(
            Path.Combine(apps, "appmanifest_324160.acf"),
            """
            "AppState" { "appid" "324160" "name" "CLANNAD" "installdir" "CLANNAD" "StateFlags" "4" }
            """,
            ct
        );
        await connections.ScanAsync("steam", apps, ct);
        var game = Assert.Single(games.Shelf());
        Assert.Equal((2, 1), (game.Accounts, game.Installations));
        var view = games.Show(game.Id);
        Assert.Contains(view.Availability, a => a.Status == "Playing");
        Assert.Contains(view.Availability, a => a.Status == "Finished");
        Assert.Contains(view.Availability, a => a.Location == Path.Combine(apps, "common", "CLANNAD"));
        games.Import(new("bangumi", "13", "CLANNAD", "https://bgm.tv/subject/13", "Original"));
        games.SetField(game.Id, "Description", "Personal");
        platform.Interrupt = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => connections.SyncAsync(bgm.Id, ct));
        Assert.Equal(2, Assert.Single(games.Shelf()).Accounts);
        platform.Empty = true;
        await connections.SyncAsync(bgm.Id, ct);
        Assert.Equal((1, 1), (games.Shelf()[0].Accounts, games.Shelf()[0].Installations));
        games.Disconnect(vn.Id);
        Assert.Contains(games.Show(game.Id).Availability, a =>
            a.Status.Contains("disconnected", StringComparison.Ordinal)
        );
        Assert.Equal("Personal", games.Show(game.Id).Fields.Single(x => x.Name == "Description").Value);
        games.SetField(game.Id, "Description", null);
        Assert.Equal("Original", games.Show(game.Id).Fields.Single(x => x.Name == "Description").Value);
        games.Unlink(game.Id, "bangumi", "13");
        Assert.Empty(games.Show(game.Id).Sources);
        Assert.Empty(new DataCatalog(new(workspace.Database)).ReadRelations(new("bangumi.game", "13", "local")));
    }

    [Fact]
    public async Task TheSamePlatformIdentityHasIndependentBindingsInTwoAccounts()
    {
        using var workspace = new PluginWorkspace();
        using var fixture = new GamePlatformFixture();
        using var http = new HttpClient(fixture);
        var access = new ResourceAccess(false);
        await using var app = PluginTestHost.Create(workspace.Database, http, access: access);
        var games = app.Services.GetRequiredService<GameLibrary>();
        var connections = app.Services.GetRequiredService<GameConnections>();
        var ct = TestContext.Current.CancellationToken;
        string origin;
        using (access.Enter(new("Alice", "alice", () => [], true)))
        {
            var bound = await connections.BindAsync("bangumi", "bgm-test", _ => throw new NotSupportedException(), ct);
            await connections.SyncAsync(bound.Id, ct);
            origin = bound.Id;
        }
        using (access.Enter(new("Bob", "bob", () => [], true)))
        {
            Assert.Empty(games.Origins());
            Assert.Empty(games.Shelf());
            Assert.Throws<KeyNotFoundException>(() => games.Disconnect(origin));
            var bound = await connections.BindAsync("bangumi", "bgm-test", _ => throw new NotSupportedException(), ct);
            Assert.NotEqual(origin, bound.Id);
            games.Disconnect(bound.Id);
        }
        using (access.Enter(new("Alice", "alice", () => [], true)))
        {
            Assert.True(Assert.Single(games.Origins()).Connected);
            Assert.Equal(1, Assert.Single(games.Shelf()).Accounts);
        }
    }

    [Fact]
    public async Task AmbiguousEditionsWithTheSameTitleStaySeparateUntilAssigned()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        var map = Path.Combine(workspace.DirectoryPath, "ids.json");
        await File.WriteAllTextAsync(map, """{"entries":[{"bangumi":[13,14],"vndb":["v4"]}]}""", ct);
        using var fixture = new GamePlatformFixture();
        using var http = new HttpClient(fixture);
        await using var app = PluginTestHost.Create(
            workspace.Database,
            http,
            configuration: new() { ["Games:IdDataset"] = map }
        );
        var connections = app.Services.GetRequiredService<GameConnections>();
        var games = app.Services.GetRequiredService<GameLibrary>();
        var bgm = await connections.BindAsync("bangumi", "bgm-test", _ => throw new NotSupportedException(), ct);
        var vn = await connections.BindAsync("vndb", "vn-test", _ => throw new NotSupportedException(), ct);
        await connections.SyncAsync(bgm.Id, ct);
        var selected = Assert.Single(games.Shelf()).Id;
        await connections.SyncAsync(vn.Id, ct);
        Assert.Equal(2, games.Shelf().Count);
        games.Assign(vn.Id, "v4", selected);
        await connections.SyncAsync(vn.Id, ct);
        Assert.Equal(2, games.Show(selected).Availability.Count);
    }

    [Fact]
    public async Task SteamVerifiesBrowserAssertionAndDistinguishesHiddenFromEmptyLibrary()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new PluginWorkspace();
        using var platform = new GamePlatformFixture();
        using var http = new HttpClient(platform);
        await using var app = PluginTestHost.Create(
            workspace.Database,
            http,
            configuration: new() { ["Games:steam:WebApiKey"] = "steam-test" }
        );
        var accounts = app.Services.GetRequiredService<GameConnections>();
        var login = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bind = accounts.BindAsync("steam", "", uri => login.SetResult(uri), ct);
        var url = await login.Task.WaitAsync(ct);
        var callback = QueryHelpers.ParseQuery(url.Query)["openid.return_to"].Single()!;
        var args = new Dictionary<string, string?>
        {
            ["openid.mode"] = "id_res",
            ["openid.op_endpoint"] = "https://steamcommunity.com/openid/login",
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.return_to"] = callback,
            ["openid.claimed_id"] = "https://steamcommunity.com/openid/id/76561198000000000",
            ["openid.identity"] = "https://steamcommunity.com/openid/id/76561198000000000",
            ["openid.signed"] = "op_endpoint,claimed_id,identity,return_to,response_nonce,assoc_handle",
            ["openid.response_nonce"] = "fixture",
            ["openid.assoc_handle"] = "fixture",
            ["openid.sig"] = "fixture",
        };
        using var browser = new HttpClient();
        using var reply = await browser.GetAsync(new Uri(QueryHelpers.AddQueryString(callback, args)), ct);
        reply.EnsureSuccessStatusCode();
        var origin = await bind;
        Assert.Equal("76561198000000000", origin.ExternalId);
        Assert.Equal(1, await accounts.SyncAsync(origin.Id, ct));
        platform.Interrupt = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.SyncAsync(origin.Id, ct));
        var library = app.Services.GetRequiredService<GameLibrary>();
        Assert.Equal(1, Assert.Single(library.Shelf()).Accounts);
        platform.Empty = true;
        Assert.Equal(0, await accounts.SyncAsync(origin.Id, ct));
        Assert.Equal(0, Assert.Single(library.Shelf()).Accounts);
    }
}
