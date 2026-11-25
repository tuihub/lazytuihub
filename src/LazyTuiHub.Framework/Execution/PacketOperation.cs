using System.Text.Json;

namespace LazyTuiHub.Framework;

public static class PacketOperation
{
    public static void RegisterRepeatable<TInput, TOutput>(
        this NodeRegistration registry,
        NodeDescriptor descriptor,
        Func<TInput, JsonElement, CancellationToken, Task<TOutput>> execute
    )
        where TInput : notnull
        where TOutput : notnull =>
        Register<TInput, TOutput>(
            registry,
            descriptor,
            async (packet, settings, token) => new(await execute(packet.Value, settings, token), packet.Reference),
            PacketExecutionMode.Repeatable
        );

    public static void RegisterOwned<TInput, TOutput>(
        this NodeRegistration registry,
        NodeDescriptor descriptor,
        Func<DataPacket<TInput>, JsonElement, CancellationToken, Task<DataPacket<TOutput>>> execute
    )
        where TInput : notnull
        where TOutput : notnull
    {
        if (string.IsNullOrWhiteSpace(descriptor.RequiredResource))
            throw new ArgumentException("An owner-bound operation must declare its resource.", nameof(descriptor));
        Register(registry, descriptor, execute, PacketExecutionMode.Owned);
    }

    private static void Register<TInput, TOutput>(
        NodeRegistration registry,
        NodeDescriptor descriptor,
        Func<DataPacket<TInput>, JsonElement, CancellationToken, Task<DataPacket<TOutput>>> execute,
        PacketExecutionMode mode
    )
        where TInput : notnull
        where TOutput : notnull
    {
        if (
            descriptor.Inputs is not [{ Delivery: PortDelivery.Streaming } input]
            || input.DataType != typeof(TInput)
            || descriptor.Outputs is not [{ Delivery: PortDelivery.Streaming } output]
            || output.DataType != typeof(TOutput)
        )
            throw new ArgumentException(
                "A packet operation requires one matching stream input and output.",
                nameof(descriptor)
            );
        registry.Register(descriptor, RunAsync, InvokeAsync, mode);

        async Task RunAsync(NodeContext context, CancellationToken token)
        {
            await foreach (var item in context.ReadEventsAsync())
            {
                var packet = (DataPacket<TInput>)((StreamInput)item).Packet;
                var result = await context.ExecutePacketAsync(packet, execute, token);
                await context.WriteAsync(output.Name, result.Value, token, result.Reference);
            }
        }

        async Task<byte[]> InvokeAsync(byte[] payload, JsonElement settings, CancellationToken token)
        {
            var result = await execute(Decode<TInput>(payload), settings, token);
            return JsonSerializer.SerializeToUtf8Bytes(result, PipelineStore.JsonOptions);
        }
    }

    internal static DataPacket<T> Decode<T>(byte[] bytes)
        where T : notnull =>
        JsonSerializer.Deserialize<DataPacket<T>>(bytes, PipelineStore.JsonOptions) is { Value: not null } packet
            ? packet
            : throw new JsonException("A packet needs a value.");
}
