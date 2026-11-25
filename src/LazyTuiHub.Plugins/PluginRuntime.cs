using System.Collections.Concurrent;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Plugins;

public sealed partial class PluginRuntime : IDisposable, IPluginAvailability
{
    private const string Present = "present";
    private const string Blocked = "blocked";
    private readonly PluginCatalog catalog;
    private readonly Dictionary<string, CapabilityGroup> capabilities = new(StringComparer.Ordinal);
    private readonly IReadOnlyCollection<string> hostGroups;
    public IEnumerable<CapabilityGroup> Capabilities => capabilities.Values;
    private readonly PluginAssemblies assemblies = new();
    private readonly Dictionary<string, PluginView> observations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PluginRegistration> configured = new(StringComparer.Ordinal);
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Plugin, string Task), string> taskStates = new();
    private readonly IConfiguration configuration;
    private readonly BusinessStorage storage;
    private readonly ResourceAccess access;
    private readonly IConnectionSource workspace;
    public DaoRegistry Daos { get; } = new();
    public RetentionRuntime Retention { get; }
    private readonly string directory;
    public WorkflowRuntime Workflows { get; }
    public string? DiscoveryError { get; private set; }

    public PluginRuntime(
        SqliteDatabase database,
        IConfiguration configuration,
        string directory,
        ResourceAccess? resourceAccess = null,
        IReadOnlyCollection<string>? hostGroups = null
    )
    {
        this.hostGroups = hostGroups ?? [];
        this.configuration = configuration;
        this.directory = Path.GetFullPath(directory);
        catalog = new(database);
        workspace = database;
        Retention = new(database, Daos);
        storage = new(database);
        access = resourceAccess ?? new(singleAccount: true);
        Workflows = new(database, RetainedOwner, access);
    }

    public void Configure(IServiceCollection services)
    {
        services.AddSingleton(access);
        services.AddSingleton<IPluginAvailability>(this);
        services.AddSingleton<IRetentionCleanup>(Retention);
        var specs = ReadManifest();
        foreach (var spec in specs)
            catalog.Observe(spec);
        var records = catalog.Read();
        foreach (var record in records)
            observations.Add(record.Spec.Id, new(record, "missing", "inactive", "No current entry declaration."));
        var pending = specs.ToDictionary(spec => spec.Id, StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var ready = pending.Values.FirstOrDefault(spec =>
                !(spec.Requires ?? []).Any(dep => pending.ContainsKey(dep.Id))
            );
            if (ready is null)
            {
                foreach (var spec in pending.Values)
                    Observe(spec.Id, "unknown", Blocked, "Plugin dependency cycle.");
                break;
            }
            pending.Remove(ready.Id);
            ConfigurePlugin(ready, services);
        }
    }

    private IReadOnlyList<PluginSpec> ReadManifest()
    {
        try
        {
            var problems = new List<string>();
            var specs = PluginManifest.Read(directory, problems.Add);
            if (problems.Count != 0)
                DiscoveryError = string.Join(Environment.NewLine, problems);
            return specs;
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            DiscoveryError = error.Message;
            return [];
        }
    }

    private void ConfigurePlugin(PluginSpec spec, IServiceCollection services)
    {
        var record = observations[spec.Id].Saved;
        var path = Path.GetFullPath(Path.Combine(directory, spec.Assembly));
        if (!File.Exists(path))
        {
            Observe(spec.Id, "missing", "inactive", $"Missing assembly: {path}");
            return;
        }
        if (!record.Enabled && !spec.Required && !IsMaintenanceTarget(spec.Id))
        {
            Observe(spec.Id, Present, "disabled", null);
            return;
        }
        if (spec.Version is not null && !NuGet.Versioning.NuGetVersion.TryParse(spec.Version, out _))
        {
            Observe(spec.Id, Present, Blocked, $"Invalid declared version '{spec.Version}'.");
            return;
        }
        var problems = (spec.Requires ?? [])
            .Select(dep => dep.Problem(configured.ContainsKey(dep.Id) ? observations[dep.Id].Saved.Spec : null))
            .OfType<string>()
            .ToArray();
        if (problems.Length > 0)
        {
            Observe(spec.Id, Present, Blocked, string.Join(Environment.NewLine, problems));
            return;
        }
        try
        {
            var entry = assemblies.Load(path, spec.EntryType);
            var registration = new PluginRegistration(
                spec.Id,
                record.Settings,
                configuration,
                storage,
                workspace,
                Daos
            );
            entry.Configure(registration);
            _ = registration.Daos.Requirements.ToDictionary(requirement => requirement.Name, StringComparer.Ordinal);
            catalog.Contributions(registration);
            foreach (var service in registration.Services)
                services.Add(service);
            configured.Add(spec.Id, registration);
            Observe(spec.Id, Present, "configured", null);
        }
        catch (Exception error)
        {
            Observe(spec.Id, "load-failed", "failed", error.GetBaseException().Message);
        }
    }

    public void Activate(IServiceProvider services, IEndpointRouteBuilder? routes = null)
    {
        if (services.GetService<ExecutionTopology>() is { } topology)
            Workflows.PlaceOn(topology);
        var pending = configured.Keys.ToHashSet(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var id in pending.ToArray())
            {
                if (!TryActivate(id, services, routes, pending))
                    continue;
                pending.Remove(id);
                progressed = true;
            }
            if (progressed)
                continue;
            foreach (var id in pending)
                Observe(
                    id,
                    Present,
                    Blocked,
                    string.Join(
                        Environment.NewLine,
                        configured[id]
                            .Daos.Problems()
                            .Concat(
                                (observations[id].Saved.Spec.Requires ?? [])
                                    .Select(dependency => dependency.Id)
                                    .Where(dependency => !active.Contains(dependency))
                                    .Select(dependency => $"Required plugin '{dependency}' is not active.")
                            )
                    )
                );
            break;
        }
        foreach (var id in active)
        {
            foreach (var workflow in configured[id].Workflows)
                Workflows.Save(workflow, replace: false);
        }
    }

    private bool TryActivate(
        string id,
        IServiceProvider services,
        IEndpointRouteBuilder? routes,
        HashSet<string> pending
    )
    {
        var registration = configured[id];
        var dependencies = (observations[id].Saved.Spec.Requires ?? []).Select(dep => dep.Id).ToArray();
        if (dependencies.Any(pending.Contains))
            return false;
        var missing = dependencies.Where(dependency => !active.Contains(dependency)).ToArray();
        if (missing.Length > 0)
        {
            Observe(id, Present, Blocked, $"Required plugins failed activation: {string.Join(", ", missing)}");
            return true;
        }
        if (registration.Daos.Problems().Count > 0)
            return false;
        if (!observations[id].Saved.Enabled && !observations[id].Saved.Spec.Required)
        {
            Observe(id, Present, "maintenance", null);
            return true;
        }
        try
        {
            ActivatePlugin(registration, services, routes);
        }
        catch (Exception error)
        {
            Observe(id, Present, "failed", error.GetBaseException().Message);
        }
        return true;
    }

    private void ActivatePlugin(
        PluginRegistration registration,
        IServiceProvider services,
        IEndpointRouteBuilder? routes
    )
    {
        var id = registration.Id;
        var nodes = new NodeRegistry();
        registration.RegisterNodes?.Invoke(nodes, services);
        var endpoints = routes is null ? null : new PluginEndpoints(routes);
        if (endpoints is not null)
            registration.MapEndpoints?.Invoke(endpoints);
        ValidateContributions(registration);
        var groups = registration.Capabilities.Values.Select(create => create(services)).ToArray();
        Workflows.Register(id, nodes);
        Retention.Observe(registration);
        catalog.Declare(id, Declaration(registration, nodes));
        endpoints?.Commit();
        Daos.Publish(id, registration.DaoServices);
        foreach (var group in groups)
            capabilities.Add(group.Name, group);
        active.Add(id);
        Observe(id, Present, "active", null);
    }

    private void ValidateContributions(PluginRegistration registration)
    {
        _ = registration.Tasks.ToDictionary(task => task.Id, StringComparer.Ordinal);
        _ = registration.Pages.ToDictionary(page => page.Id, StringComparer.Ordinal);
        Daos.Validate(registration.DaoServices);
        var duplicate = capabilities
            .Keys.Concat(hostGroups)
            .Intersect(registration.Capabilities.Keys, StringComparer.Ordinal)
            .FirstOrDefault();
        if (duplicate is not null)
            throw new InvalidOperationException($"Command already registered: {duplicate}.");
    }

    private static PluginDeclaration Declaration(PluginRegistration registration, NodeRegistry nodes) =>
        new(
            nodes
                .Descriptors.Select(node => new DeclaredNode(
                    node.Type,
                    node.Module,
                    node.Inputs.Select(DescribePort).ToArray(),
                    node.Outputs.Select(DescribePort).ToArray(),
                    node.RequiredResource
                ))
                .ToArray(),
            registration.DataTypes.ToArray(),
            registration.Capabilities.Keys.ToArray(),
            registration.Tasks.Select(task => new DeclaredTask(task.Id, task.Workflows.ToArray())).ToArray(),
            registration.Hooks.Cleanup is not null,
            registration.Hooks.Uninstall is not null
        )
        {
            DaoRequirements = registration.Daos.Requirements.ToArray(),
            DaoOffers = registration.DaoServices.Select(service => service.Capability).ToArray(),
            Pages = registration.Pages.ToArray(),
        };

    private static DeclaredPort DescribePort(PortDescriptor port) =>
        new(port.Name, port.DataType.FullName ?? port.DataType.Name, port.Delivery.ToString());

    public IReadOnlyList<PluginView> List() =>
        catalog
            .Read()
            .Select(record =>
                observations.TryGetValue(record.Spec.Id, out var current)
                    ? current with
                    {
                        Saved = record,
                    }
                    : new PluginView(record, "missing", "inactive", "No current implementation.")
            )
            .ToArray();

    public bool IsActive(string id) => active.Contains(id);

    public void SetEnabled(string id, bool enabled)
    {
        access.Require(ResourceUri.SystemControl("operations"), AccessMode.Write);
        catalog.SetEnabled(id, enabled);
    }

    public void SetSettings(string id, JsonElement settings) => catalog.SetSettings(id, settings);

    public string UnavailableGroup(string name)
    {
        var owner = List().FirstOrDefault(p => p.Saved.Declaration.Commands.Contains(name, StringComparer.Ordinal));
        return owner is null
            ? $"Unknown operation group '{name}'."
            : $"Group '{name}' is unavailable; plugin '{owner.Saved.Spec.Id}': {owner.Reason}";
    }

    public async Task InvokeHookAsync(string id, string operation, IServiceProvider services, CancellationToken token)
    {
        if (
            !configured.TryGetValue(id, out var registration)
            || (
                !active.Contains(id)
                && !string.Equals(observations[id].Activation, "maintenance", StringComparison.Ordinal)
            )
        )
            throw new InvalidOperationException($"Plugin '{id}' has no available implementation for its hook.");
        var hook = operation switch
        {
            "cleanup" => registration.Hooks.Cleanup,
            "uninstall" => registration.Hooks.Uninstall,
            _ => throw new ArgumentException("Choose cleanup or uninstall.", nameof(operation)),
        };
        if (hook is null)
            throw new InvalidOperationException($"Plugin '{id}' does not provide a {operation} hook.");
        await hook(services, token);
    }

    public async Task<IReadOnlyList<PluginTaskView>> TasksAsync(CancellationToken token)
    {
        var result = new List<PluginTaskView>();
        foreach (var plugin in List())
        {
            var id = plugin.Saved.Spec.Id;
            foreach (var task in plugin.Saved.Declaration.Tasks)
            {
                var problem = active.Contains(id) ? null : plugin.Reason ?? plugin.Activation;
                problem ??= await TaskProblemAsync(task, token);
                var state = problem is null ? taskStates.GetValueOrDefault((id, task.Id)) ?? "ready" : "unavailable";
                result.Add(new(id, task, state, problem));
            }
        }
        return result;
    }

    private async Task<string?> TaskProblemAsync(DeclaredTask task, CancellationToken token)
    {
        foreach (var name in task.Workflows)
        {
            try
            {
                var description = await Workflows.DescribeAsync(Workflows.Get(name), token);
                if (description.Problems is [var issue, ..])
                    return $"{issue.NodeType} (plugin {issue.PluginId ?? "unknown"}): {issue.Reason}";
            }
            catch (KeyNotFoundException error)
            {
                return error.Message;
            }
        }
        return null;
    }

    internal void MarkUnstartedTasksStopped()
    {
        foreach (var id in active)
        {
            foreach (var task in configured[id].Tasks)
                taskStates.TryAdd((id, task.Id), "stopped");
        }
    }

    internal async Task RunTasksAsync(IServiceProvider services, CancellationToken token)
    {
        var runnable = (await TasksAsync(token))
            .Where(task => string.Equals(task.State, "ready", StringComparison.Ordinal))
            .ToArray();
        await Task.WhenAll(
            runnable.Select(async task =>
            {
                taskStates[(task.PluginId, task.Declaration.Id)] = "running";
                try
                {
                    var implementation = configured[task.PluginId]
                        .Tasks.Single(item => string.Equals(item.Id, task.Declaration.Id, StringComparison.Ordinal));
                    await implementation.Run(services, token);
                    taskStates[(task.PluginId, task.Declaration.Id)] = "completed";
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    taskStates[(task.PluginId, task.Declaration.Id)] = "stopped";
                }
                catch (Exception error)
                {
                    taskStates[(task.PluginId, task.Declaration.Id)] = $"failed: {error.Message}";
                    LogTaskFailure(
                        services.GetRequiredService<ILogger<PluginRuntime>>(),
                        task.PluginId,
                        task.Declaration.Id,
                        error
                    );
                }
            })
        );
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} task {TaskId} failed.")]
    private static partial void LogTaskFailure(ILogger logger, string pluginId, string taskId, Exception exception);

    private bool IsMaintenanceTarget(string id) =>
        string.Equals(configuration["command"], "plugins", StringComparison.Ordinal)
        && configuration["action"] is "cleanup" or "uninstall"
        && string.Equals(configuration["plugin"], id, StringComparison.Ordinal);

    private string? RetainedOwner(string type) =>
        catalog
            .Read()
            .FirstOrDefault(record =>
                record.Declaration.Nodes.Any(node => string.Equals(node.Type, type, StringComparison.Ordinal))
            )
            ?.Spec.Id;

    private void Observe(string id, string availability, string activation, string? reason) =>
        observations[id] = new(observations[id].Saved, availability, activation, reason);

    public void Dispose() => assemblies.Dispose();
}
