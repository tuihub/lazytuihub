using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class DaoTests
{
    [Fact]
    public async Task OneRecordToolServesIndependentPluginTablesAndExplicitExpiry()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var registry = app.Services.GetRequiredService<PluginRuntime>().Daos;
        var records = registry.Resolve<IRecordDao>(new("details", DaoMode.Unified, DaoContracts.Records));
        var now = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
        var first = new DaoTable("lazytuihub.plugin.tuihub.rss", "details");
        var other = new DaoTable("another-business", "details");
        records.CreateTable(first);
        records.CreateTable(other);
        records.Put(first, "expired", new { message = "old" }, now);
        records.Put(first, "long-lived", new { message = "saved" });
        records.Put(first, "keep", new { message = "promoted" }, now.AddMinutes(1));
        records.Put(other, "independent", new { message = "other" }, now);
        Assert.Equal(2, records.Read<JsonElement>(first, now).Count);
        Assert.Equal(3, records.Read<JsonElement>(first, now, includeExpired: true).Count);
        Assert.True(records.Keep(first, "keep"));
        Assert.False(records.Keep(first, "unknown"));
        Assert.Equal(1, records.DeleteExpired(first, now.AddHours(1)));
        Assert.Equal(2, records.Read<JsonElement>(first, now.AddHours(1)).Count);
        Assert.Single(records.Read<JsonElement>(other, now, includeExpired: true));
        Assert.Equal(1, records.DeleteExpired(other, now));
        Assert.Empty(records.Read<JsonElement>(other, now, includeExpired: true));
    }

    [Fact]
    public async Task RssDetailsUseExplicitRetentionAndCanBePromotedBeforeDeletion()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var scope = new DaoScope("clock-test", app.Services.GetRequiredService<PluginRuntime>().Daos);
        scope.Requirements.Add(new("details", DaoMode.Unified, DaoContracts.Records));
        var now = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
        var clock = new ManualClock(now);
        var details = new LazyTuiHub.Rss.RssDetails(scope, TimeSpan.FromMinutes(1), clock);
        details.Capture(new(new("https://example.com/feed"), [1, 2, 3], new("rss.feed", "example")));
        var saved = Assert.Single(details.Read());
        Assert.Equal(now.AddMinutes(1), saved.ExpiresAt);
        Assert.Equal(3L, saved.Value.Bytes);
        clock.Now = now.AddMinutes(1);
        Assert.Empty(details.Read());
        Assert.Single(details.Read(includeExpired: true));
        Assert.True(details.Keep(saved.Key));
        Assert.Null(Assert.Single(details.Read()).ExpiresAt);
    }

    [Fact]
    public async Task NamespacesPreserveCaseBoundariesAndTreatNamesAsIdentifiers()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var registry = app.Services.GetRequiredService<PluginRuntime>().Daos;
        var records = registry.Resolve<IRecordDao>(new("records", DaoMode.Unified, DaoContracts.Records));
        DaoTable[] targets =
        [
            new("RSS", "test_records"),
            new("lazytuihub.plugin.tuihub.rss", "test_records"),
            new("a_b", "c"),
            new("a", "b_c"),
            new("世界", "x\"; DROP TABLE pipelines; --"),
        ];
        Assert.Equal(targets.Length, targets.Select(DaoNames.Table).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var target in targets)
        {
            records.CreateTable(target);
            records.Put(target, "' OR 1=1 --", target);
            Assert.Equal(target, Assert.Single(records.Read<DaoTable>(target, DateTimeOffset.UtcNow)).Value);
        }
        Assert.Throws<SqliteException>(() => records.CreateTable(new("lazytuihub.plugin.tuihub.rss", "feeds")));
        Assert.NotEmpty(app.Services.GetRequiredService<WorkflowRuntime>().List());
    }

    [Fact]
    public void MatchingIncludesModeInstanceBackendAndAmbiguity()
    {
        var registry = new DaoRegistry();
        var first = new object();
        var second = new object();
        registry.Publish("first", [new(new(DaoMode.Unified, "example", "store", "first-backend"), first)]);
        registry.Publish("second", [new(new(DaoMode.Unified, "example", "store", "second-backend"), second)]);
        Assert.Contains("multiple", registry.Problem(new("x", DaoMode.Unified, "example", "store")));
        Assert.Same(first, registry.Resolve<object>(new("x", DaoMode.Unified, "example", "store", "first-backend")));
        Assert.NotNull(registry.Problem(new("x", DaoMode.Native, "example", "store", "first-backend")));
        Assert.NotNull(registry.Problem(new("x", DaoMode.Unified, "example", "absent", "first-backend")));
        Assert.Throws<ArgumentException>(() =>
            registry.Publish("duplicate", [new(new(DaoMode.Unified, "example", "store", "first-backend"), second)])
        );
        Assert.Equal(2, registry.List().Count);
    }

    [Fact]
    public async Task SearchAndBusinessMetadataShareTheWorkspaceTransaction()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var registry = app.Services.GetRequiredService<PluginRuntime>().Daos;
        var source = registry.Resolve<IConnectionSource>(
            new("database", DaoMode.Native, DaoContracts.Relational, Backend: "sqlite")
        );
        var search = registry.Resolve<ISqliteSearch>(
            new("search", DaoMode.BackendSpecific, DaoContracts.SqliteSearch, Backend: "sqlite")
        );
        var table = new DaoTable("probe", "search");
        search.CreateTable(table);
        var submissions = new BusinessStorage(source);
        Assert.Throws<SqliteException>(() => submissions.Write(source, (_, transaction) =>
            {
                search.Index(table, "ghost", "rollback searchable", transaction);
                return new BusinessChange<bool>(
                    true,
                    [new("probe", "ghost")],
                    [new(new("probe", "ghost"), new("missing", "id"), "ref")]
                );
            }));
        Assert.Empty(search.Search(table, "searchable"));
        submissions.Write(source, (_, transaction) =>
        {
            search.Index(table, "present", "committed searchable", transaction);
            return new BusinessChange<bool>(true, [new("probe", "present")], []);
        });
        Assert.Equal("present", Assert.Single(search.Search(table, "searchable")));
        var other = new SqliteDatabase(Path.Combine(workspace.DirectoryPath, "other.db"));
        Assert.Throws<ArgumentException>(() =>
            submissions.Write(other, (_, _) => new BusinessChange<bool>(true, [], []))
        );
    }

    [Fact]
    public async Task RetentionOnlyTouchesDelegatedTablesAndPauseSurvivesStartup()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        var now = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
        var delegated = new DaoTable("lazytuihub.plugin.tuihub.rss", "fetch_details");
        var privateTable = new DaoTable("lazytuihub.plugin.tuihub.rss", "private_details");
        await using (var app = PluginTestHost.Create(workspace.Database, http))
        {
            var runtime = app.Services.GetRequiredService<PluginRuntime>();
            var records = runtime.Daos.Resolve<IRecordDao>(new("details", DaoMode.Unified, DaoContracts.Records));
            records.CreateTable(privateTable);
            records.Put(privateTable, "keep-private", true, now);
            records.Put(delegated, "expired", true, now);
            records.Put(delegated, "keep", true);
            runtime.Retention.SetEnabled("lazytuihub.plugin.tuihub.rss", delegated, false);
            runtime.Retention.Clean(now, TestContext.Current.CancellationToken);
            Assert.Equal(2, records.Read<bool>(delegated, now, includeExpired: true).Count);
        }
        await using var restarted = PluginTestHost.Create(workspace.Database, http);
        var manager = restarted.Services.GetRequiredService<PluginRuntime>();
        Assert.Equal("paused", Assert.Single(manager.Retention.List()).State);
        var tools = manager.Daos.Resolve<IRecordDao>(new("records", DaoMode.Unified, DaoContracts.Records));
        manager.Retention.SetEnabled("lazytuihub.plugin.tuihub.rss", delegated, true);
        var result = Assert.Single(manager.Retention.Clean(now, TestContext.Current.CancellationToken));
        Assert.Equal(1, result.Saved.LastDeleted);
        Assert.Null(result.Saved.LastError);
        Assert.Single(tools.Read<bool>(delegated, now, includeExpired: true));
        Assert.Single(tools.Read<bool>(privateTable, now, includeExpired: true));
        manager.SetEnabled("lazytuihub.plugin.tuihub.rss", false);
        tools.Put(delegated, "expires-while-disabled", true, now);
        await using var disabled = PluginTestHost.Create(workspace.Database, http);
        var inactiveOwner = disabled.Services.GetRequiredService<PluginRuntime>();
        Assert.Equal(
            "disabled",
            inactiveOwner.List().Single(view => view.Saved.Spec.Id == "lazytuihub.plugin.tuihub.rss").Activation
        );
        Assert.Equal(
            1,
            Assert.Single(inactiveOwner.Retention.Clean(now, TestContext.Current.CancellationToken)).Saved.LastDeleted
        );
        Assert.Single(tools.Read<bool>(delegated, now, includeExpired: true));
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
