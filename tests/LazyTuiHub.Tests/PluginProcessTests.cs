using System.Net;
using System.Text.Json;
using Google.Protobuf;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Client;
using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class PluginProcessTests
{
    [Fact]
    public async Task ColdStartupTogglesAndMissingAssembliesRetainTheWorkspace()
    {
        using var workspace = new PluginWorkspace();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var fixture = builder.Build();
        fixture.MapGet("/feed", () => Results.Text(RssDemo.FeedXml, "application/rss+xml"));
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await workspace.SuccessAsync("plugins", "list");
            var feed = JsonParser.Default.Parse<Feed>(
                await workspace.SuccessAsync("rss", "add", "--feed-url", fixture.Urls.Single() + "/feed")
            );
            await workspace.SuccessAsync("rss", "refresh", "--name", feed.Name);
            var articles = JsonParser.Default.Parse<ListArticlesResponse>(
                await workspace.SuccessAsync("rss", "articles")
            );
            var article = articles.Articles[0];
            await workspace.SuccessAsync("rss", "read", "--name", article.Name);
            var definition = await workspace.JsonAsync<PipelineDefinition>(
                "workflow",
                "definition",
                "--pipeline-name",
                "rss.ingest"
            );
            definition = definition with
            {
                BufferCapacity = 1,
                Nodes = definition
                    .Nodes.Select(node =>
                        node with
                        {
                            Settings = JsonSerializer.SerializeToElement(
                                new { preserved = new { text = "opaque settings" }, interval = "00:00:07" }
                            ),
                        }
                    )
                    .ToArray(),
            };
            var file = Path.Combine(workspace.DirectoryPath, "workflow.json");
            await File.WriteAllTextAsync(
                file,
                JsonSerializer.Serialize(definition, PipelineStore.JsonOptions),
                TestContext.Current.CancellationToken
            );
            await workspace.SuccessAsync("workflow", "save", "--pipeline-file", file);
            var saved = await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest");
            var settings = Path.Combine(workspace.DirectoryPath, "settings.json");
            await File.WriteAllTextAsync(
                settings,
                "{\"refreshInterval\":\"00:00:07\",\"custom\":true}",
                TestContext.Current.CancellationToken
            );
            await workspace.SuccessAsync("plugins", "configure", "--plugin", "rss", "--file", settings);
            var relations = await workspace.SuccessAsync(
                "workflow",
                "relations",
                "--data-type",
                "rss.feed",
                "--data-key",
                feed.Name.Split('/')[1]
            );

            await workspace.SuccessAsync("plugins", "disable", "--plugin", "rss");
            var disabled = await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss");
            Assert.False(disabled.Saved.Enabled);
            Assert.Equal("disabled", disabled.Activation);
            Assert.NotEmpty(disabled.Saved.Declaration.Nodes);
            Assert.Equal("00:00:07", disabled.Saved.Settings.GetProperty("refreshInterval").GetString());
            Assert.Equal(
                saved,
                await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest")
            );
            Assert.Equal(
                relations,
                await workspace.SuccessAsync(
                    "workflow",
                    "relations",
                    "--data-type",
                    "rss.feed",
                    "--data-key",
                    feed.Name.Split('/')[1]
                )
            );
            Assert.Equal(1, (await workspace.RunAsync("rss", "feeds")).Code);
            var blocked = await workspace.RunAsync("workflow", "plan", "--pipeline-name", "rss.ingest");
            Assert.Equal(1, blocked.Code);
            Assert.Contains("rss", blocked.Output);
            Assert.DoesNotContain("rss.parse", await workspace.SuccessAsync("workflow", "nodes"));
            Assert.Contains("unavailable", await workspace.SuccessAsync("plugins", "list"));

            await workspace.SuccessAsync("plugins", "enable", "--plugin", "rss");
            var dll = Path.Combine(workspace.Plugins, "rss", "LazyTuiHub.Rss.dll");
            File.Move(dll, dll + ".absent");
            var missing = await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss");
            Assert.True(missing.Saved.Enabled);
            Assert.Equal("missing", missing.Availability);
            Assert.Equal(disabled.Saved.Declaration.Nodes.Count, missing.Saved.Declaration.Nodes.Count);
            Assert.Equal(
                saved,
                await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest")
            );
            File.Move(dll + ".absent", dll);
            var restored = await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss");
            Assert.Equal("active", restored.Activation);
            Assert.True(restored.Saved.Settings.GetProperty("custom").GetBoolean());
            var retained = JsonParser.Default.Parse<ListArticlesResponse>(
                await workspace.SuccessAsync("rss", "articles")
            );
            Assert.Equal(2, retained.Articles.Count);
            Assert.True(retained.Articles.Single(value => value.Name == article.Name).Read);
            Assert.Equal(
                saved,
                await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest")
            );
            Assert.Equal(1, (await workspace.RunAsync("plugins", "cleanup", "--plugin", "rss")).Code);
            Assert.Equal(1, (await workspace.RunAsync("plugins", "disable", "--plugin", "core")).Code);

            var core = Path.Combine(workspace.Plugins, "core", "LazyTuiHub.CorePlugin.dll");
            File.Move(core, core + ".absent");
            Assert.Equal(
                "missing",
                (await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "core")).Availability
            );
            Assert.Equal(1, (await workspace.RunAsync("workflow", "plan", "--pipeline-name", "rss.ingest")).Code);
            Assert.Contains(
                "core",
                (await workspace.RunAsync("workflow", "plan", "--pipeline-name", "rss.ingest")).Output
            );
            Assert.Contains(
                "rss.article",
                await workspace.SuccessAsync(
                    "workflow",
                    "relations",
                    "--data-type",
                    "rss.feed",
                    "--data-key",
                    feed.Name.Split('/')[1]
                )
            );
            File.Delete(Path.Combine(workspace.Plugins, "plugins.json"));
            Assert.Equal(
                "missing",
                (await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "rss")).Availability
            );
            Assert.Equal(
                saved,
                await workspace.SuccessAsync("workflow", "definition", "--pipeline-name", "rss.ingest")
            );
        }
        finally
        {
            await fixture.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DisabledBusinessRoutesDisappearAndMissingCoreStillHostsDiagnostics()
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
        await File.WriteAllTextAsync(
            manifestFile,
            JsonSerializer.Serialize(manifest, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
        string[] args = ["--demo", "true", "--database", workspace.Database, "--plugin-directory", workspace.Plugins];
        await using (var initial = ApplicationHost.Create(args))
            initial.Services.GetRequiredService<PluginRuntime>().SetEnabled("rss", false);
        await using (var disabled = ApplicationHost.Create(args))
        {
            Assert.Null(disabled.Services.GetService<IRssApi>());
            await disabled.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                using var http = new HttpClient { BaseAddress = new(disabled.Urls.Single()) };
                using var response = await http.GetAsync("v1/feeds", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                var greeting = await new GreetingHttpClient(http).GenerateGreetingAsync(
                    new(),
                    TestContext.Current.CancellationToken
                );
                Assert.Equal("Hello, World!", greeting.Message);
            }
            finally
            {
                await disabled.StopAsync(CancellationToken.None);
            }
        }
        manifest = manifest.Select(spec => spec.Required ? spec with { Assembly = "absent.dll" } : spec).ToArray();
        await File.WriteAllTextAsync(
            manifestFile,
            JsonSerializer.Serialize(manifest, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
        await using var minimal = ApplicationHost.Create(args);
        await minimal.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(
                "missing",
                minimal
                    .Services.GetRequiredService<PluginRuntime>()
                    .List()
                    .Single(view => view.Saved.Spec.Required)
                    .Availability
            );
            Assert.NotEmpty(minimal.Services.GetRequiredService<WorkflowRuntime>().List());
            Assert.Null(minimal.Services.GetService<IGreetingApi>());
        }
        finally
        {
            await minimal.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnknownNodesAndFreshMissingCoreRemainDiagnosable()
    {
        using var workspace = new PluginWorkspace();
        File.Delete(Path.Combine(workspace.Plugins, "core", "LazyTuiHub.CorePlugin.dll"));
        Assert.Equal(
            "missing",
            (await workspace.JsonAsync<PluginView>("plugins", "inspect", "--plugin", "core")).Availability
        );
        Assert.Contains("core", (await workspace.RunAsync("workflow", "plan", "--pipeline-name", "rss.ingest")).Output);
        var file = Path.Combine(workspace.DirectoryPath, "unknown.json");
        var definition = new PipelineDefinition(
            "imported",
            [PipelineNode.Create("unknown", "other.custom", new { opaque = true })],
            []
        );
        await File.WriteAllTextAsync(
            file,
            JsonSerializer.Serialize(definition, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
        var saved = await workspace.JsonAsync<WorkflowDescription>("workflow", "save", "--pipeline-file", file);
        Assert.False(saved.Executable);
        Assert.Null(Assert.Single(saved.Problems).PluginId);
        Assert.True(saved.Definition.Nodes[0].Settings.GetProperty("opaque").GetBoolean());
    }
}
