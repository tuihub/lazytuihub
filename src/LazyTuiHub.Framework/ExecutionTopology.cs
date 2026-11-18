namespace LazyTuiHub.Framework;

public sealed class ExecutionTopology(
    string instance,
    IReadOnlySet<string> resources,
    IReadOnlyList<IExecutionPeer> peers
)
{
    public const string PrimaryWorkspace = "workspace.primary";
    public static ExecutionTopology Standalone { get; } =
        new("local", new HashSet<string>(StringComparer.Ordinal) { PrimaryWorkspace }, []);

    public ExecutionSnapshot LocalInventory { get; } =
        new(instance, resources, [], new Dictionary<string, string>(StringComparer.Ordinal));

    public async Task<ExecutionSnapshot> InspectAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (peers.Count == 0)
            return LocalInventory;
        var reports = await Task.WhenAll(
            peers.Select(async peer =>
            {
                try
                {
                    var inventory = await peer.InspectAsync(token);
                    return (Peer: peer, Inventory: (WorkerInventory?)inventory, Failure: (string?)null);
                }
                catch (InstanceUnavailableException error)
                {
                    token.ThrowIfCancellationRequested();
                    return (Peer: peer, Inventory: (WorkerInventory?)null, Failure: (string?)error.Message);
                }
            })
        );
        var available = reports
            .Where(report => report.Inventory is not null)
            .Select(report => (report.Peer, Inventory: report.Inventory!))
            .OrderBy(report => report.Inventory.Instance, StringComparer.Ordinal)
            .ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal) { instance };
        var duplicate = available.Select(report => report.Inventory.Instance).FirstOrDefault(id => !names.Add(id));
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate execution instance '{duplicate}'.");
        return new(
            instance,
            resources,
            available,
            reports
                .Where(report => report.Failure is not null)
                .ToDictionary(report => report.Peer.Endpoint, report => report.Failure!, StringComparer.Ordinal)
        );
    }
}
