namespace LazyTuiHub.Framework;

public sealed class ExecutionSnapshot
{
    private readonly string instance;
    private readonly IReadOnlySet<string> resources;
    private readonly (IExecutionPeer Peer, WorkerInventory Inventory)[] workers;
    public IReadOnlyDictionary<string, string> Unavailable { get; }

    internal ExecutionSnapshot(
        string instance,
        IReadOnlySet<string> resources,
        (IExecutionPeer Peer, WorkerInventory Inventory)[] workers,
        IReadOnlyDictionary<string, string> unavailable
    )
    {
        this.instance = instance;
        this.resources = resources;
        this.workers = workers;
        Unavailable = unavailable;
    }

    internal ExecutionTarget[] Place(
        string id,
        RegisteredNode node,
        IReadOnlyList<string> requirements,
        ResourceAccess? access
    )
    {
        var candidates = new List<ExecutionTarget>();
        if (requirements.All(resources.Contains))
            candidates.Add(new(instance, null));
        if (node.InvokePacket is not null)
            candidates.AddRange(
                workers
                    .Where(worker =>
                        (
                            node.Mode == PacketExecutionMode.Owned
                                ? worker.Inventory.OwnedOperations
                                : worker.Inventory.RepeatableOperations
                        ).Contains(node.Descriptor.Type) && requirements.All(worker.Inventory.Resources.Contains)
                    )
                    .Select(worker => new ExecutionTarget(worker.Inventory.Instance, worker.Peer))
            );
        var permitted = candidates.ToArray();
        if (node.Mode == PacketExecutionMode.Repeatable && access is not null && permitted.Length > 0)
        {
            var jobs = access.Check(permitted.Select(candidate => ResourceUri.DeviceJobs(candidate.Instance)));
            permitted = permitted
                .Where(candidate => jobs[ResourceUri.DeviceJobs(candidate.Instance)].HasFlag(AccessMode.Write))
                .ToArray();
        }
        if (candidates.Count != 0 && permitted.Length == 0)
            throw new AccessDeniedException();
        if (permitted.Length == 0)
            throw new ArgumentException(
                $"No eligible instance for node '{id}' ({node.Descriptor.Type}) with resources [{string.Join(", ", requirements)}]. {Diagnostics()}",
                nameof(node)
            );
        if (node.Mode == PacketExecutionMode.Owned && candidates.Count > 1)
            throw new ArgumentException(
                $"Ambiguous ownership of '{node.Descriptor.RequiredResource}' for node '{id}' ({node.Descriptor.Type}): {string.Join(", ", candidates.Select(candidate => candidate.Instance))}.",
                nameof(node)
            );
        return permitted;
    }

    internal string Diagnostics() =>
        string.Join(" ", Unavailable.Select(pair => $"Inventory unavailable at {pair.Key}: {pair.Value}"));
}
