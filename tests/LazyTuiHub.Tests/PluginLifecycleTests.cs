using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class PluginLifecycleTests
{
    [Fact]
    public async Task HooksAreExplicitAndDisabledMaintenanceDoesNotActivateCapabilities()
    {
        using var workspace = new PluginWorkspace();
        var spec = new PluginSpec(
            "probe",
            "Probe",
            typeof(ProbePlugin).Assembly.Location,
            typeof(ProbePlugin).FullName!
        );
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Plugins, "plugins.json"),
            JsonSerializer.Serialize(new[] { spec }, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
        using var http = new HttpClient();
        await using (var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins))
        {
            var runtime = app.Services.GetRequiredService<PluginRuntime>();
            var state = app.Services.GetRequiredService<ProbeState>();
            Assert.Equal(0, state.Cleanups);
            Assert.Equal(0, state.Uninstalls);
            await runtime.InvokeHookAsync("probe", "cleanup", app.Services, TestContext.Current.CancellationToken);
            Assert.Equal(1, state.Cleanups);
            Assert.Equal("active", Assert.Single(runtime.List()).Activation);
            runtime.SetEnabled("probe", false);
            Assert.Single(runtime.Workflows.Registry.Descriptors);
            using var service = new PluginTaskService(runtime, app.Services);
            await service.StartAsync(TestContext.Current.CancellationToken);
            await state.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await service.StopAsync(TestContext.Current.CancellationToken);
            await state.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal("stopped", Assert.Single(runtime.Tasks()).State);
        }
        await using (var disabled = PluginTestHost.Create(workspace.Database, http, workspace.Plugins))
        {
            var runtime = disabled.Services.GetRequiredService<PluginRuntime>();
            Assert.Null(disabled.Services.GetService<ProbeState>());
            Assert.Empty(runtime.Workflows.Registry.Descriptors);
            Assert.Equal("unavailable", Assert.Single(runtime.Tasks()).State);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runtime.InvokeHookAsync("probe", "cleanup", disabled.Services, TestContext.Current.CancellationToken)
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
                ["plugin"] = "probe",
            }
        );
        var manager = maintenance.Services.GetRequiredService<PluginRuntime>();
        Assert.Equal("maintenance", Assert.Single(manager.List()).Activation);
        Assert.Empty(manager.Workflows.Registry.Descriptors);
        Assert.Equal("unavailable", Assert.Single(manager.Tasks()).State);
        var maintainedState = maintenance.Services.GetRequiredService<ProbeState>();
        Assert.Equal(0, maintainedState.Cleanups);
        Assert.Equal(0, maintainedState.Uninstalls);
        await manager.InvokeHookAsync(
            "probe",
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
            "probe",
            "Probe",
            typeof(ProbePlugin).Assembly.Location,
            typeof(ProbePlugin).FullName!
        );
        PluginSpec[] manifest =
        [
            probe with
            {
                Id = "missing",
                Assembly = "absent.dll",
            },
            probe with
            {
                Id = "depends-missing",
                Requires = ["missing"],
            },
            probe with
            {
                Id = "failed",
                EntryType = typeof(FailedActivationPlugin).FullName!,
            },
            probe with
            {
                Id = "depends-failed",
                Requires = ["failed"],
            },
            probe,
        ];
        await File.WriteAllTextAsync(
            Path.Combine(workspace.Plugins, "plugins.json"),
            JsonSerializer.Serialize(manifest, PipelineStore.JsonOptions),
            TestContext.Current.CancellationToken
        );
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http, workspace.Plugins);
        var runtime = app.Services.GetRequiredService<PluginRuntime>();
        var states = runtime.List().ToDictionary(view => view.Saved.Spec.Id, StringComparer.Ordinal);
        Assert.Equal("active", states["probe"].Activation);
        Assert.Equal("missing", states["missing"].Availability);
        Assert.Equal("failed", states["failed"].Activation);
        Assert.Equal("blocked", states["depends-missing"].Activation);
        Assert.Equal("blocked", states["depends-failed"].Activation);
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
