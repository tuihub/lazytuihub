using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace LazyTuiHub.Framework;

public sealed class PipelineRunner(NodeRegistry registry)
{
    public void Validate(PipelineDefinition definition)
    {
        if (definition.BufferCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition), "The queue capacity must be positive.");
        var nodes = definition.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (nodes.Count == 0)
            throw new ArgumentException("A pipeline needs at least one node.", nameof(definition));
        var incoming = nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var edge in definition.Edges)
        {
            if (!nodes.TryGetValue(edge.Source, out var source) || !nodes.TryGetValue(edge.Target, out var target))
                throw new ArgumentException($"Unknown endpoint: {edge.Source} -> {edge.Target}.", nameof(definition));
            var output = registry.Get(source.Type).Descriptor.OutputType;
            var input = registry.Get(target.Type).Descriptor.InputType;
            if (output is null || input is null || !input.IsAssignableFrom(output))
                throw new ArgumentException(
                    $"Incompatible connection: {edge.Source} -> {edge.Target}.",
                    nameof(definition)
                );
            incoming[edge.Target]++;
        }
        if (definition.Edges.Distinct().Count() != definition.Edges.Count)
            throw new ArgumentException("A pipeline cannot contain duplicate connections.", nameof(definition));
        foreach (var node in nodes.Values)
        {
            var descriptor = registry.Get(node.Type).Descriptor;
            if ((descriptor.InputType is null) != (incoming[node.Id] == 0))
                throw new ArgumentException(
                    $"Node {node.Id} has no compatible input or is a connected source.",
                    nameof(definition)
                );
            if (
                descriptor.OutputType is not null
                && !definition.Edges.Any(edge => edge.Source.Equals(node.Id, StringComparison.Ordinal))
            )
                throw new ArgumentException($"Connect the output of {node.Id} to a consumer.", nameof(definition));
        }
        ValidateAcyclic(definition, incoming, nodes.Count);
    }

    private static void ValidateAcyclic(PipelineDefinition definition, Dictionary<string, int> incoming, int count)
    {
        var ready = new Queue<string>(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (ready.TryDequeue(out var id))
        {
            visited++;
            foreach (
                var target in definition
                    .Edges.Where(edge => edge.Source.Equals(id, StringComparison.Ordinal))
                    .Select(edge => edge.Target)
            )
                if (--incoming[target] == 0)
                    ready.Enqueue(target);
        }
        if (visited != count)
            throw new ArgumentException("Pipeline connections must form a DAG.", nameof(definition));
    }

    public async Task RunAsync(
        PipelineDefinition definition,
        PipelineContext? context = null,
        CancellationToken cancellationToken = default
    )
    {
        Validate(definition);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var states = definition.Nodes.ToDictionary(
            node => node.Id,
            node => new ExecutionNode(
                node,
                definition.Edges.Count(edge => edge.Target.Equals(node.Id, StringComparison.Ordinal)),
                definition.BufferCapacity
            ),
            StringComparer.Ordinal
        );
        Exception? failure = null;
        await Task.WhenAll(states.Values.Select(state => Task.Run(() => ExecuteAsync(state), CancellationToken.None)));
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        cancellationToken.ThrowIfCancellationRequested();

        async Task ExecuteAsync(ExecutionNode state)
        {
            var targets = definition
                .Edges.Where(edge => edge.Source.Equals(state.Node.Id, StringComparison.Ordinal))
                .Select(edge => states[edge.Target])
                .ToArray();
            try
            {
                stopping.Token.ThrowIfCancellationRequested();
                var input = state.Inbox.Reader.ReadAllAsync(stopping.Token);
                var output = registry.Get(state.Node.Type).Run(input, state.Node, context ?? new(), stopping.Token);
                await foreach (var packet in output.WithCancellation(stopping.Token))
                {
                    foreach (var target in targets)
                        await target.Inbox.Writer.WriteAsync(packet, stopping.Token);
                }
            }
            catch (Exception error)
            {
                var reported =
                    error is OperationCanceledException ? error : new PipelineExecutionException(state.Node.Id, error);
                Interlocked.CompareExchange(ref failure, reported, null);
                await stopping.CancelAsync();
            }
            finally
            {
                foreach (var target in targets)
                    if (Interlocked.Decrement(ref target.Producers) == 0)
                        target.Inbox.Writer.TryComplete();
            }
        }
    }

    private sealed class ExecutionNode
    {
        public ExecutionNode(PipelineNode node, int producers, int capacity)
        {
            Node = node;
            Producers = producers;
            Inbox = Channel.CreateBounded<DataPacket>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = producers <= 1,
                }
            );
            if (producers == 0)
                Inbox.Writer.TryComplete();
        }

        public PipelineNode Node { get; }
        public Channel<DataPacket> Inbox { get; }
        public int Producers;
    }
}
