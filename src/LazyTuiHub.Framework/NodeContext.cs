using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed class NodeContext
{
    private readonly Func<string, DataPacket, string?, CancellationToken, ValueTask> publish;
    private readonly Dictionary<string, PortDescriptor> outputs;
    private readonly IAsyncEnumerable<NodeInput> input;
    private readonly PlannedNode planned;

    private readonly ExecutionSnapshot inventory;
    private readonly ResourceAccess? access;

    internal NodeContext(
        PlannedNode node,
        PipelineContext run,
        IAsyncEnumerable<NodeInput> input,
        Func<string, DataPacket, string?, CancellationToken, ValueTask> publish,
        ExecutionSnapshot inventory,
        ResourceAccess? access
    )
    {
        planned = node;
        this.inventory = inventory;
        this.access = access;
        Node = node.Definition;
        Run = run;
        this.input = input;
        this.publish = publish;
        outputs = node.Descriptor.Outputs.ToDictionary(port => port.Name, StringComparer.Ordinal);
    }

    public PipelineNode Node { get; }
    public PipelineContext Run { get; }

    public IAsyncEnumerable<NodeInput> ReadEventsAsync() => input;

    internal async Task<DataPacket<TOutput>> ExecutePacketAsync<TInput, TOutput>(
        DataPacket<TInput> packet,
        Func<DataPacket<TInput>, JsonElement, CancellationToken, Task<DataPacket<TOutput>>> execute,
        CancellationToken token
    )
        where TInput : notnull
        where TOutput : notnull
    {
        byte[]? wireInput = null;
        var failures = new List<string>();
        foreach (var target in planned.Targets)
        {
            token.ThrowIfCancellationRequested();
            if (!AuthorizeAttempt(target))
            {
                Report("denied", target.Instance, "The device-use grant is unavailable.");
                continue;
            }
            Report("attempt", target.Instance);
            try
            {
                DataPacket<TOutput> result;
                if (target.Peer is null)
                    result = await execute(packet, Node.Settings, token);
                else
                {
                    wireInput ??= JsonSerializer.SerializeToUtf8Bytes(packet, PipelineStore.JsonOptions);
                    var bytes = await target.Peer.InvokeAsync(
                        target.Instance,
                        Node.Type,
                        planned.Requirements,
                        planned.Registration.Mode!.Value,
                        Node.Settings,
                        wireInput,
                        token
                    );
                    result = PacketOperation.Decode<TOutput>(bytes);
                }
                Report("delivered", target.Instance);
                return result;
            }
            catch (InstanceUnavailableException error)
            {
                token.ThrowIfCancellationRequested();
                if (planned.Registration.Mode == PacketExecutionMode.Owned)
                {
                    Report("unconfirmed", target.Instance, error.Message);
                    throw new UnconfirmedExecutionException(
                        $"No response confirming '{Node.Type}' on {target.Instance}. Its outcome is unknown. {error.Message}",
                        error
                    );
                }
                Report("unavailable", target.Instance, error.Message);
                failures.Add($"{target.Instance}: {error.Message}");
            }
            catch (PacketRejectedException error)
            {
                Report("rejected", target.Instance, error.Message);
                throw new PacketRejectedException($"'{Node.Type}' on {target.Instance}: {error.Message}", error);
            }
        }
        token.ThrowIfCancellationRequested();
        if (failures.Count == 0)
            throw new AccessDeniedException();
        throw new InstanceUnavailableException(
            $"No instance completed '{Node.Type}'. {string.Join("; ", failures)} {inventory.Diagnostics()}"
        );

        void Report(string state, string instance, string? detail = null) =>
            Run.Progress?.Invoke(new(Node.Id, state, Instance: instance, Detail: detail, Reference: packet.Reference));
    }

    private bool AuthorizeAttempt(ExecutionTarget target)
    {
        if (access is null)
            return true;
        if (planned.Descriptor.DataAccess is { } data)
            access.Require(data.UriFor(access.Account), data.Mode);
        return planned.Registration.Mode != PacketExecutionMode.Repeatable
            || access.Allows(ResourceUri.DeviceJobs(target.Instance), AccessMode.Write);
    }

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
