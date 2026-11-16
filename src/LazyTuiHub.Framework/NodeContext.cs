namespace LazyTuiHub.Framework;

public sealed class NodeContext
{
    private readonly Func<string, DataPacket, string?, CancellationToken, ValueTask> publish;
    private readonly Dictionary<string, PortDescriptor> outputs;
    private readonly IAsyncEnumerable<NodeInput> input;

    internal NodeContext(
        PlannedNode node,
        PipelineContext run,
        IAsyncEnumerable<NodeInput> input,
        Func<string, DataPacket, string?, CancellationToken, ValueTask> publish
    )
    {
        Node = node.Definition;
        Run = run;
        this.input = input;
        this.publish = publish;
        outputs = node.Descriptor.Outputs.ToDictionary(port => port.Name, StringComparer.Ordinal);
    }

    public PipelineNode Node { get; }
    public PipelineContext Run { get; }

    public IAsyncEnumerable<NodeInput> ReadEventsAsync() => input;

    public ValueTask WriteAsync<T>(
        string port,
        T value,
        CancellationToken cancellationToken,
        DataReference? reference = null,
        string? messageKey = null
    )
        where T : notnull
    {
        var descriptor = outputs[port];
        if (descriptor.DataType != typeof(T))
            throw new ArgumentException(
                $"Output '{port}' declares {descriptor.DataType}, not {typeof(T)}.",
                nameof(value)
            );
        if ((descriptor.Delivery == PortDelivery.Synchronized) != (messageKey is not null))
            throw new ArgumentException(
                $"Output '{port}' requires a key exactly when synchronized.",
                nameof(messageKey)
            );
        return publish(port, new DataPacket<T>(value, reference), messageKey, cancellationToken);
    }
}
