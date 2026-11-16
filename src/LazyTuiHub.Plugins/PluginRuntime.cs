using System.Collections.Concurrent;
using System.Text.Json;
using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Plugins;

public sealed partial class PluginRuntime : IDisposable
{
    private const string Present = "present";
    private const string Blocked = "blocked";
    private readonly PluginCatalog catalog;
    private readonly PluginAssemblies assemblies = new();
    private readonly Dictionary<string, PluginView> observations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PluginRegistration> configured = new(StringComparer.Ordinal);
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Plugin, string Task), string> taskStates = new();
    private readonly IConfiguration configuration;
    private readonly BusinessStorage storage;
    private readonly IConnectionSource workspace;
    public DaoRegistry Daos { get; } = new();
    public RetentionRuntime Retention { get; }
    private readonly string directory;
    public WorkflowRuntime Workflows { get; }
    public string? DiscoveryError { get; private set; }

    public PluginRuntime(SqliteDatabase database, IConfiguration configuration, string directory)
    {
        this.configuration = configuration;
        this.directory = Path.GetFullPath(directory);
        catalog = new(database);
        workspace = database;
        Retention = new(database, Daos);
        storage = new(database);
        Workflows = new(database, RetainedOwner);
    }

    public void Configure(IServiceCollection services)
    {
        var specs = ReadManifest();
        foreach (var spec in specs)
            catalog.Observe(spec);
        var records = catalog.Read();
        foreach (var record in records)
            observations.Add(record.Spec.Id, new(record, "missing", "inactive", "No current entry declaration."));
        var pending = specs.ToDictionary(spec => spec.Id, StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var ready = pending.Values.FirstOrDefault(spec => !(spec.Requires ?? []).Any(pending.ContainsKey));
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

    private PluginSpec[] ReadManifest()
    {
        var path = Path.Combine(directory, "plugins.json");
        try
        {
            return JsonSerializer.Deserialize<PluginSpec[]>(File.ReadAllText(path), PipelineStore.JsonOptions)
                ?? throw new JsonException("The plugin manifest is empty.");
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
        var missing = (spec.Requires ?? []).Where(id => !configured.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            Observe(spec.Id, Present, Blocked, $"Unavailable required plugins: {string.Join(", ", missing)}");
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

    public void Activate(WebApplication app)
    {
        var pending = configured.Keys.ToHashSet(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var id in pending.ToArray())
            {
                if (!TryActivate(id, app, pending))
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

    private bool TryActivate(string id, WebApplication app, HashSet<string> pending)
    {
        var registration = configured[id];
        var dependencies = observations[id].Saved.Spec.Requires ?? [];
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
            ActivatePlugin(registration, app);
        }
        catch (Exception error)
        {
            Observe(id, Present, "failed", error.GetBaseException().Message);
        }
        return true;
    }

    private void ActivatePlugin(PluginRegistration registration, WebApplication app)
    {
        var id = registration.Id;
        var nodes = new NodeRegistry();
        registration.RegisterNodes?.Invoke(nodes, app.Services);
        var endpoints = new PluginEndpoints(app);
        registration.MapEndpoints?.Invoke(endpoints);
        ValidateContributions(registration);
        Workflows.Register(id, nodes);
        Retention.Observe(registration);
        catalog.Declare(id, Declaration(registration, nodes));
        endpoints.Commit();
        Daos.Publish(id, registration.DaoServices);
        active.Add(id);
        Observe(id, Present, "active", null);
    }

    private void ValidateContributions(PluginRegistration registration)
    {
        _ = registration.Commands.ToDictionary(command => command.Name, StringComparer.Ordinal);
        _ = registration.Tasks.ToDictionary(task => task.Id, StringComparer.Ordinal);
        Daos.Validate(registration.DaoServices);
        var duplicate = active
            .SelectMany(owner => configured[owner].Commands)
            .Select(command => command.Name)
            .Intersect(registration.Commands.Select(command => command.Name), StringComparer.Ordinal)
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
                    node.Outputs.Select(DescribePort).ToArray()
                ))
                .ToArray(),
            registration.DataTypes.ToArray(),
            registration.Commands.Select(command => command.Name).ToArray(),
            registration.Tasks.Select(task => new DeclaredTask(task.Id, task.Workflows.ToArray())).ToArray(),
            registration.Hooks.Cleanup is not null,
            registration.Hooks.Uninstall is not null
        )
        {
            DaoRequirements = registration.Daos.Requirements.ToArray(),
            DaoOffers = registration.DaoServices.Select(service => service.Capability).ToArray(),
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

    public void SetEnabled(string id, bool enabled) => catalog.SetEnabled(id, enabled);

    public void SetSettings(string id, JsonElement settings) => catalog.SetSettings(id, settings);

    public PluginCommand Command(string name)
    {
        var commands = active
            .SelectMany(id => configured[id].Commands)
            .Where(command => string.Equals(command.Name, name, StringComparison.Ordinal))
            .ToArray();
        if (commands.Length == 1)
            return commands[0];
        var owner = List()
            .FirstOrDefault(view => view.Saved.Declaration.Commands.Contains(name, StringComparer.Ordinal));
        throw new InvalidOperationException(
            $"Command '{name}' is unavailable; plugin '{owner?.Saved.Spec.Id ?? "unknown"}': {owner?.Reason}"
        );
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

    public IReadOnlyList<PluginTaskView> Tasks() =>
        List()
            .SelectMany(plugin =>
                plugin.Saved.Declaration.Tasks.Select(task =>
                {
                    if (!active.Contains(plugin.Saved.Spec.Id))
                        return new PluginTaskView(
                            plugin.Saved.Spec.Id,
                            task,
                            "unavailable",
                            plugin.Reason ?? plugin.Activation
                        );
                    var problem = task.Workflows.SelectMany(name => DescribeTaskWorkflow(name)).FirstOrDefault();
                    return new PluginTaskView(
                        plugin.Saved.Spec.Id,
                        task,
                        problem is null
                            ? taskStates.GetValueOrDefault((plugin.Saved.Spec.Id, task.Id)) ?? "ready"
                            : "unavailable",
                        problem
                    );
                })
            )
            .ToArray();

    private IEnumerable<string> DescribeTaskWorkflow(string name)
    {
        PipelineDefinition definition;
        try
        {
            definition = Workflows.Get(name);
        }
        catch (KeyNotFoundException error)
        {
            return [error.Message];
        }
        return Workflows
            .Describe(definition)
            .Problems.Select(problem =>
                $"{problem.NodeType} (plugin {problem.PluginId ?? "unknown"}): {problem.Reason}"
            );
    }

    internal async Task RunTasksAsync(IServiceProvider services, CancellationToken token)
    {
        var runnable = Tasks().Where(task => string.Equals(task.State, "ready", StringComparison.Ordinal)).ToArray();
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
