using System.Runtime.CompilerServices;

namespace LazyTuiHub.Framework;

public sealed class NodeRegistry
{
    private readonly Dictionary<string, RegisteredNode> nodes = new(StringComparer.Ordinal);

    public IEnumerable<NodeDescriptor> Descriptors => nodes.Values.Select(node => node.Descriptor);

    public void RegisterSource<T>(
        string type,
        string module,
        Func<PipelineNode, PipelineContext, CancellationToken, IAsyncEnumerable<DataPacket<T>>> run
    )
        where T : class =>
        Add(new(type, module, null, typeof(T)), (input, node, context, token) => run(node, context, token));

    public void RegisterTransform<TInput, TOutput>(
        string type,
        string module,
        Func<
            DataPacket<TInput>,
            PipelineNode,
            PipelineContext,
            CancellationToken,
            IAsyncEnumerable<DataPacket<TOutput>>
        > run
    )
        where TInput : class
        where TOutput : class
    {
        Add(new(type, module, typeof(TInput), typeof(TOutput)), Transform);

        // Each registration keeps its typed operator behind the common graph boundary.
        async IAsyncEnumerable<DataPacket> Transform(
            IAsyncEnumerable<DataPacket> input,
            PipelineNode node,
            PipelineContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await foreach (var packet in input.WithCancellation(cancellationToken))
            {
                var typed =
                    packet as DataPacket<TInput> ?? new DataPacket<TInput>((TInput)packet.Payload, packet.Reference);
                await foreach (
                    var output in run(typed, node, context, cancellationToken).WithCancellation(cancellationToken)
                )
                    yield return output;
            }
        }
    }

    public void RegisterSink<T>(
        string type,
        string module,
        Func<T, DataReference?, PipelineNode, PipelineContext, CancellationToken, ValueTask> run
    )
        where T : class
    {
        Add(new(type, module, typeof(T), null), Consume);

        async IAsyncEnumerable<DataPacket> Consume(
            IAsyncEnumerable<DataPacket> input,
            PipelineNode node,
            PipelineContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await foreach (var packet in input.WithCancellation(cancellationToken))
                await run((T)packet.Payload, packet.Reference, node, context, cancellationToken);
            yield break;
        }
    }

    internal RegisteredNode Get(string type) =>
        nodes.TryGetValue(type, out var node)
            ? node
            : throw new ArgumentException($"Unknown node type: {type}.", nameof(type));

    private void Add(NodeDescriptor descriptor, NodeOperator run)
    {
        if (!nodes.TryAdd(descriptor.Type, new(descriptor, run)))
            throw new ArgumentException($"Node type already registered: {descriptor.Type}.", nameof(descriptor));
    }
}
