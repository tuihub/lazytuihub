using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class PluginDependencyTests
{
    private const string Probe = "lazytuihub.plugin.tests.compatibility";
    private const string Games = "lazytuihub.plugin.tuihub.games";

    internal sealed class Frontend : IPluginFrontend
    {
        public void Configure(PluginServices registration) => registration.Pages.Add(new("probe", "Probe"));
    }

    [Fact]
    public async Task LocalAndRemoteColdStartUseTheSameEmbeddedCompatibilityDeclaration()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        var selected = workspace
            .ReadPlugins()
            .Select(spec => new PluginSelection(spec.Id, Path.GetFileName(spec.Assembly), spec.Required))
            .ToList();
        var file = typeof(Frontend).Assembly.Location;
        var probeFolder = Path.Combine(workspace.Plugins, Probe);
        Directory.CreateDirectory(probeFolder);
        File.Copy(file, Path.Combine(probeFolder, Path.GetFileName(file)));
        selected.Insert(0, new(Probe, Path.GetFileName(file)));
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Plugins, "plugins.json"),
            JsonSerializer.Serialize(selected, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );

        await using (
            var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins, frontend: FrontendKind.Cli)
        )
        {
            var plugins = app.Services.GetRequiredService<PluginRuntime>();
            var state = plugins.List().Single(view => view.Saved.Spec.Id == Probe);
            Assert.Equal("blocked", state.Activation);
            Assert.Contains(Games, state.Reason);
            Assert.Contains("[2.0.0,3.0.0)", state.Reason);
            Assert.Contains("1.0.0", state.Reason);
            Assert.True(plugins.IsActive(Games));
            plugins.SetEnabled(Games, false);
        }
        await using (
            var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins, frontend: FrontendKind.Cli)
        )
        {
            var plugins = app.Services.GetRequiredService<PluginRuntime>();
            Assert.True(plugins.IsActive(Probe));
            Assert.False(plugins.IsActive(Games));
        }
        VerifyRemote(workspace.Plugins, false);
        using var missingPeer = new PluginWorkspace();
        var remoteProbe = Path.Combine(missingPeer.Plugins, Probe);
        Directory.CreateDirectory(remoteProbe);
        File.Copy(file, Path.Combine(remoteProbe, Path.GetFileName(file)));
        File.Copy(
            Path.Combine(workspace.Plugins, "plugins.json"),
            Path.Combine(missingPeer.Plugins, "plugins.json"),
            overwrite: true
        );
        File.Delete(
            Path.Combine(missingPeer.Plugins, missingPeer.ReadPlugins().Single(spec => spec.Id == Games).Assembly)
        );
        VerifyRemote(missingPeer.Plugins, true);

        void VerifyRemote(string directory, bool expectPage)
        {
            using var assemblies = new PluginAssemblies();
            var problems = new List<string>();
            var pages = FrontendEntryPoints.ConfigureRemote(
                assemblies,
                directory,
                FrontendKind.Cli,
                new ServiceCollection(),
                [],
                problems.Add
            );
            Assert.Equal(expectPage, pages.Any(page => page.Owner == Probe));
            Assert.Equal(
                !expectPage,
                problems.Any(problem =>
                    problem.Contains(Probe, StringComparison.Ordinal)
                    && problem.Contains("[2.0.0,3.0.0)", StringComparison.Ordinal)
                )
            );
        }
    }

    private sealed class ActivationProbe : IApplicationPlugin
    {
        public void Configure(PluginRegistration registration) =>
            registration.RegisterNodes = (_, services) =>
                services.GetRequiredService<List<string>>().Add(registration.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibilityChecksVersionsWithoutOrderingThePlugins(bool reverse)
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        var a = new PluginSpec(
            Probe,
            "A",
            typeof(ActivationProbe).Assembly.Location,
            new(typeof(ActivationProbe).FullName),
            Version: "1.0.0"
        );
        var b = a with { Id = Games, Name = "B" };
        var compatibleA = a with { CompatibleWith = [new(b.Id, "[1.0.0,2.0.0)")] };
        var compatibleB = b with { CompatibleWith = [new(a.Id, "[1.0.0]")] };

        var baseline = await Start(a, b);
        Assert.Equal(2, baseline.Count);
        Assert.Equal(baseline, await Start(compatibleA, b));
        Assert.Equal(baseline, await Start(compatibleA, compatibleB));
        Assert.Equal([b.Id], await Start(compatibleA, compatibleB with { Version = "2.0.0" }));
        Assert.Equal([b.Id, a.Id], await Start(a with { Requires = [new(b.Id)] }, compatibleB));

        async Task<List<string>> Start(PluginSpec left, PluginSpec right)
        {
            var order = new List<string>();
            await using var app = PluginTestHost.Create(
                workspace.Database,
                http,
                workspace.Plugins,
                configure: services => services.AddSingleton(order),
                specs: reverse ? [right, left] : [left, right]
            );
            return order;
        }
    }

    [Fact]
    public void ConfigurationFailureAndCyclesOnlyPropagateAlongRequiredEdges()
    {
        var peer = new PluginSpec("peer", "Peer", "", new(), Version: "1.2.0-rc.2+build.4");
        var optional = peer with { Id = "optional", CompatibleWith = [new(peer.Id, "[1.2.0-rc.1,1.2.0)")] };
        var required = peer with { Id = "required", Requires = [new(peer.Id)] };
        var calls = new List<string>();
        var blocked = new Dictionary<string, string>();
        PluginLoadPlan.Configure(
            [optional, required, peer],
            spec =>
            {
                calls.Add(spec.Id);
                return spec.Id != peer.Id;
            },
            (spec, reason) => blocked.Add(spec.Id, reason)
        );
        Assert.Equal(new[] { "optional", "peer" }, calls.Order(StringComparer.Ordinal));
        Assert.Contains("peer", Assert.Single(blocked).Value);

        calls.Clear();
        blocked.Clear();
        var cycle = peer with { Requires = [new("cycle")] };
        PluginLoadPlan.Configure(
            [optional, required, cycle, peer with { Id = "cycle", Requires = [new("peer")] }],
            spec =>
            {
                calls.Add(spec.Id);
                return true;
            },
            (spec, reason) => blocked.Add(spec.Id, reason)
        );
        Assert.Equal(["optional"], calls);
        Assert.Contains("dependency cycle", blocked["cycle"]);
        Assert.Contains("dependency cycle", blocked["peer"]);
        Assert.Contains("peer", blocked["required"]);
    }
}
