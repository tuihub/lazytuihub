using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class PluginLifecycleTests
{
    [Fact]
    public async Task ManifestIdentitySeparatesPublishersAndRequiresCanonicalSpelling()
    {
        using var workspace = new PluginWorkspace();
        var steam = workspace.ReadPlugins().Single(p => p.Id == "lazytuihub.plugin.tuihub.steam");
        var other = steam with { Id = "lazytuihub.plugin.other.steam" };
        await workspace.SelectPluginsAsync([
            steam,
            other,
            steam with
            {
                Id = "steam",
            },
            other with
            {
                Id = "lazytuihub.plugin.Other.tools",
            },
        ]);
        var diagnostics = new List<string>();
        var loaded = PluginManifest.Read(workspace.Plugins, diagnostics.Add);
        Assert.Equal([steam.Id, other.Id], loaded.Select(spec => spec.Id));
        Assert.Equal(["Invalid plugin ID 'steam'.", "Invalid plugin ID 'lazytuihub.plugin.Other.tools'."], diagnostics);
    }

    [Theory]
    [InlineData("[1.0.0]", "1.0.0", true)]
    [InlineData("[1.0.0,2.0.0)", "1.5.0", true)]
    [InlineData("[1.0.0,2.0.0)", "2.0.0", false)]
    [InlineData("1.*", "2.0.0", false)]
    [InlineData("[1.0.0]", null, false)]
    [InlineData("invalid-range", "1.0.0", false)]
    public async Task ProviderActivationUsesDeclaredVersions(string range, string? installed, bool accepted)
    {
        using var workspace = new PluginWorkspace();
        var manifest = workspace.ReadPlugins().ToDictionary(p => p.Id, StringComparer.Ordinal);
        await workspace.WriteManifestAsync(manifest["lazytuihub.plugin.tuihub.games"] with { Version = installed });
        await workspace.WriteManifestAsync(
            manifest["lazytuihub.plugin.tuihub.steam"] with
            {
                Requires = [new("lazytuihub.plugin.tuihub.games", range)],
            }
        );
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins);
        var plugins = app.Services.GetRequiredService<PluginRuntime>().List();
        var steam = plugins.Single(p => p.Saved.Spec.Id == "lazytuihub.plugin.tuihub.steam");
        Assert.Equal(accepted ? "active" : "blocked", steam.Activation);
        if (!accepted)
            Assert.Contains(range, steam.Reason);
        Assert.Equal("active", plugins.Single(p => p.Saved.Spec.Id == "lazytuihub.plugin.tuihub.bangumi").Activation);
    }

    [Fact]
    public async Task HooksAreExplicitAndDisabledMaintenanceDoesNotActivateCapabilities()
    {
        using var workspace = new PluginWorkspace();
        var spec = new PluginSpec(
            "lazytuihub.plugin.tests.probe",
            "Probe",
            typeof(ProbePlugin).Assembly.Location,
            typeof(ProbePlugin).FullName!
        );
        await workspace.SelectPluginsAsync(new[] { spec });
        using var http = new HttpClient();
        await using (var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins))
        {
            var runtime = app.Services.GetRequiredService<PluginRuntime>();
            var state = app.Services.GetRequiredService<ProbeState>();
            Assert.Equal(0, state.Cleanups);
            Assert.Equal(0, state.Uninstalls);
            await runtime.InvokeHookAsync(
                "lazytuihub.plugin.tests.probe",
                "cleanup",
                app.Services,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(1, state.Cleanups);
            Assert.Equal("active", Assert.Single(runtime.List()).Activation);
            runtime.SetEnabled("lazytuihub.plugin.tests.probe", false);
            Assert.Single(runtime.Workflows.Registry.Descriptors);
            using var service = new PluginTaskService(runtime, app.Services);
            await service.StartAsync(TestContext.Current.CancellationToken);
            await state.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await service.StopAsync(TestContext.Current.CancellationToken);
            await state.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(
                "stopped",
                Assert.Single(await runtime.TasksAsync(TestContext.Current.CancellationToken)).State
            );
        }
        await using (var disabled = PluginTestHost.Create(workspace.Database, http, workspace.Plugins))
        {
            var runtime = disabled.Services.GetRequiredService<PluginRuntime>();
            Assert.Null(disabled.Services.GetService<ProbeState>());
            Assert.Empty(runtime.Workflows.Registry.Descriptors);
            Assert.Equal(
                "unavailable",
                Assert.Single(await runtime.TasksAsync(TestContext.Current.CancellationToken)).State
            );
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runtime.InvokeHookAsync(
                    "lazytuihub.plugin.tests.probe",
                    "cleanup",
                    disabled.Services,
                    TestContext.Current.CancellationToken
                )
            );
        }
        await using var maintenance = PluginTestHost.Create(
            workspace.Database,
            http,
            workspace.Plugins,
            new()
            {
                ["command"] = "plugins",
                ["action"] = "uninstall",
                ["plugin"] = "lazytuihub.plugin.tests.probe",
            }
        );
        var manager = maintenance.Services.GetRequiredService<PluginRuntime>();
        Assert.Equal("maintenance", Assert.Single(manager.List()).Activation);
        Assert.Empty(manager.Workflows.Registry.Descriptors);
        Assert.Equal(
            "unavailable",
            Assert.Single(await manager.TasksAsync(TestContext.Current.CancellationToken)).State
        );
        var maintainedState = maintenance.Services.GetRequiredService<ProbeState>();
        Assert.Equal(0, maintainedState.Cleanups);
        Assert.Equal(0, maintainedState.Uninstalls);
        await manager.InvokeHookAsync(
            "lazytuihub.plugin.tests.probe",
            "uninstall",
            maintenance.Services,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, maintainedState.Uninstalls);
    }

    [Fact]
    public async Task RequiredServicesBlockDependentsWithoutBlockingIndependentPlugins()
    {
        using var workspace = new PluginWorkspace();
        var probe = new PluginSpec(
            "lazytuihub.plugin.tests.probe",
            "Probe",
            typeof(ProbePlugin).Assembly.Location,
            typeof(ProbePlugin).FullName!
        );
        PluginSpec[] manifest =
        [
            probe with
            {
                Id = "lazytuihub.plugin.tests.missing",
                Assembly = "absent.dll",
            },
            probe with
            {
                Id = "lazytuihub.plugin.tests.depends-missing",
                Requires = [new("lazytuihub.plugin.tests.missing")],
            },
            probe with
            {
                Id = "lazytuihub.plugin.tests.failed",
                EntryType = typeof(FailedActivationPlugin).FullName!,
            },
            probe with
            {
                Id = "lazytuihub.plugin.tests.depends-failed",
                Requires = [new("lazytuihub.plugin.tests.failed")],
            },
            probe,
        ];
        await workspace.SelectPluginsAsync(manifest);
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins);
        var runtime = app.Services.GetRequiredService<PluginRuntime>();
        var states = runtime.List().ToDictionary(view => view.Saved.Spec.Id, StringComparer.Ordinal);
        Assert.Equal("active", states["lazytuihub.plugin.tests.probe"].Activation);
        Assert.Equal("missing", states["lazytuihub.plugin.tests.missing"].Availability);
        Assert.Equal("failed", states["lazytuihub.plugin.tests.failed"].Activation);
        Assert.Equal("blocked", states["lazytuihub.plugin.tests.depends-missing"].Activation);
        Assert.Equal("blocked", states["lazytuihub.plugin.tests.depends-failed"].Activation);
        Assert.Equal("probe.node", Assert.Single(runtime.Workflows.Registry.Descriptors).Type);
    }

    [Fact]
    public void BusinessSubmissionAndFrameworkRegistrationCommitTogether()
    {
        using var workspace = new PluginWorkspace();
        var source = new SqliteDatabase(workspace.Database);
        var storage = new BusinessStorage(source);
        using (var connection = source.Open())
        {
            using var schema = connection.CreateCommand();
            schema.CommandText = "CREATE TABLE example_business (id TEXT PRIMARY KEY)";
            schema.ExecuteNonQuery();
        }
        Assert.Throws<SqliteException>(() => storage.Write(source, (connection, transaction) =>
            {
                using var write = connection.CreateCommand();
                write.Transaction = transaction;
                write.CommandText = "INSERT INTO example_business VALUES ('entry')";
                write.ExecuteNonQuery();
                return new BusinessChange<bool>(
                    true,
                    [new("example.item", "entry")],
                    [new(new("example.item", "entry"), new("missing.item", "absent"), "points-to")]
                );
            }));
        using var check = source.Open();
        using var query = check.CreateCommand();
        query.CommandText = "SELECT (SELECT count(*) FROM example_business) + (SELECT count(*) FROM data_objects)";
        Assert.Equal(0L, query.ExecuteScalar());
    }
}
