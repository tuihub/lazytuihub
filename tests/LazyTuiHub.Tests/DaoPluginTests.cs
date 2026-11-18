using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class DaoPluginTests
{
    [Fact]
    public async Task MissingProviderBlocksConsumerAndRestorationPreservesDeclarations()
    {
        using var workspace = new PluginWorkspace();
        await workspace.SuccessAsync("plugins", "list");
        var status = await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss");
        Assert.Equal(2, status.Saved.Declaration.DaoRequirements.Count);
        Assert.Equal("active", status.Activation);
        await workspace.SuccessAsync("rss", "add", "--feed-url", "https://example.com/feed");
        var saved = await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest");
        var dll = Path.Combine(workspace.Plugins, "storage", "LazyTuiHub.Storage.Sqlite.dll");
        File.Move(dll, dll + ".absent");
        var missing = await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss");
        Assert.Equal("blocked", missing.Activation);
        Assert.Contains(DaoContracts.Records, missing.Reason);
        Assert.Contains(
            DaoContracts.SqliteSearch,
            (await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "articles")).Reason
        );
        Assert.Equal(2, missing.Saved.Declaration.DaoRequirements.Count);
        Assert.Equal(saved, await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest"));
        var report = await workspace.SuccessAsync("dao", "list");
        Assert.Contains("unavailable", report);
        Assert.Contains("fetch_details", report);
        Assert.Equal(
            "active",
            (await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "core")).Activation
        );
        File.Move(dll + ".absent", dll);
        Assert.Equal(
            "active",
            (await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss")).Activation
        );
        Assert.Contains("https://example.com/feed", await workspace.SuccessAsync("rss", "feeds"));
        Assert.Equal(saved, await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest"));
    }

    [Fact]
    public async Task FirstStartupExplainsMissingCapabilitiesAndActivationDoesNotDependOnManifestOrder()
    {
        using var workspace = new PluginWorkspace();
        var manifestFile = Path.Combine(workspace.Plugins, "plugins.json");
        var manifest = JsonSerializer.Deserialize<PluginSpec[]>(
            await File.ReadAllTextAsync(manifestFile, TestContext.Current.CancellationToken),
            PipelineStore.JsonOptions
        )!;
        manifest = manifest
            .Select(spec => spec with { Assembly = Path.Combine(AppContext.BaseDirectory, "plugins", spec.Assembly) })
            .ToArray();
        manifest = manifest
            .Select(spec =>
                spec.Id == "storage.sqlite"
                    ? spec with
                    {
                        Assembly = typeof(NativeOnlyStoragePlugin).Assembly.Location,
                        EntryType = typeof(NativeOnlyStoragePlugin).FullName!,
                    }
                    : spec
            )
            .ToArray();
        await File.WriteAllTextAsync(
            manifestFile,
            JsonSerializer.Serialize(manifest, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins);
        var runtime = app.Services.GetRequiredService<PluginRuntime>();
        var rss = runtime.List().Single(view => view.Saved.Spec.Id == "rss");
        Assert.Equal("blocked", rss.Activation);
        Assert.Equal(2, rss.Saved.Declaration.DaoRequirements.Count);
        Assert.Contains(
            DaoContracts.SqliteSearch,
            runtime.List().Single(view => view.Saved.Spec.Id == "articles").Reason
        );
        Assert.Contains(DaoContracts.Records, rss.Reason);
        Assert.Single(runtime.Daos.List());
        Assert.Equal("active", runtime.List().Single(view => view.Saved.Spec.Id == "core").Activation);
    }
}
