using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LazyTuiHub.Framework;

internal sealed class NodeInbox
{
    private readonly string nodeId;
    private readonly Action<NodeProgress>? progress;
    private readonly Channel<Delivery> inbox;
    private readonly Dictionary<string, int> producers;
    private readonly string[] synchronized;
    private readonly HashSet<string> closed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Queue<DataPacket>>> pending = new(StringComparer.Ordinal);
    private int remaining;

    public NodeInbox(PlannedNode node, PipelinePlan plan, Action<NodeProgress>? progress)
    {
        nodeId = node.Definition.Id;
        this.progress = progress;
        producers = node.Descriptor.Inputs.ToDictionary(
            port => port.Name,
            port =>
                plan.Connections.Count(edge =>
                    string.Equals(edge.Target, nodeId, StringComparison.Ordinal)
                    && string.Equals(edge.TargetPort, port.Name, StringComparison.Ordinal)
                ),
            StringComparer.Ordinal
        );
        synchronized = node
            .Descriptor.Inputs.Where(port => port.Delivery == PortDelivery.Synchronized)
            .Select(port => port.Name)
            .ToArray();
        remaining = producers.Values.Sum();
        inbox = Channel.CreateBounded<Delivery>(
            new BoundedChannelOptions(plan.BufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            }
        );
        if (remaining == 0)
            inbox.Writer.TryComplete();
    }

    public void Stop() =>
        inbox.Writer.TryComplete(new InvalidOperationException($"Node '{nodeId}' stopped accepting input."));

    public ValueTask WriteAsync(string port, DataPacket packet, string? key, CancellationToken token) =>
        inbox.Writer.WriteAsync(new(port, packet, key), token);

    public async ValueTask CompleteAsync(string port, CancellationToken token)
    {
        bool completed;
        lock (producers)
            completed = --producers[port] == 0;
        if (completed)
            await inbox.Writer.WriteAsync(new(port, null, null), token);
        if (Interlocked.Decrement(ref remaining) == 0)
            inbox.Writer.TryComplete();
    }

    public async IAsyncEnumerable<NodeInput> ReadAsync([EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var delivery in inbox.Reader.ReadAllAsync(token))
        {
            if (delivery.Packet is null)
                closed.Add(delivery.Port);
            else if (delivery.Key is null)
                yield return new StreamInput(delivery.Port, delivery.Packet);
            else
            {
                var group = Enqueue(delivery);
                while (synchronized.All(port => group[port].Count > 0))
                {
                    var values = synchronized.ToDictionary(
                        port => port,
                        port => group[port].Dequeue(),
                        StringComparer.Ordinal
                    );
                    if (group.Values.All(queue => queue.Count == 0))
                        pending.Remove(delivery.Key);
                    progress?.Invoke(new(nodeId, "synchronized", delivery.Key));
                    yield return new SynchronizedInput(
                        delivery.Key,
                        new ReadOnlyDictionary<string, DataPacket>(values)
                    );
                }
            }
            if (delivery.Packet is null || delivery.Key is not null)
                ExplainPending();
        }
    }

    private Dictionary<string, Queue<DataPacket>> Enqueue(Delivery delivery)
    {
        if (!pending.TryGetValue(delivery.Key!, out var group))
        {
            group = synchronized.ToDictionary(port => port, _ => new Queue<DataPacket>(), StringComparer.Ordinal);
            pending.Add(delivery.Key!, group);
        }
        group[delivery.Port].Enqueue(delivery.Packet!);
        return group;
    }

    private void ExplainPending()
    {
        foreach (var (key, group) in pending)
        {
            var missing = synchronized.Where(port => group[port].Count == 0).ToArray();
            if (missing.Any(closed.Contains))
                throw new InvalidOperationException(
                    $"Synchronized input '{key}' cannot complete; missing ports: {string.Join(", ", missing)}."
                );
            progress?.Invoke(new(nodeId, "waiting-synchronization", key, missing));
        }
    }

    private sealed record Delivery(string Port, DataPacket? Packet, string? Key);
}
