using LazyTuiHub.Framework;

namespace LazyTuiHub.Nodes;

public static class StreamNodes
{
    public const string Input = "input";
    public const string Output = "output";

    public static void RegisterOutput<T>(NodeRegistry registry, string type, string module)
        where T : notnull =>
        Sink<T>(registry, type, module, (packet, context, token) =>
            context.Run.Output?.Invoke(packet, token) ?? ValueTask.CompletedTask
        );

    public static void Source<T>(
        NodeRegistry registry,
        string type,
        string module,
        Func<NodeContext, CancellationToken, IAsyncEnumerable<DataPacket<T>>> run,
        string? requiredResource = null
    )
        where T : notnull =>
        registry.Register(
            new(type, module, [], [PortDescriptor.Stream<T>(Output)], requiredResource),
            async (context, token) =>
            {
                await foreach (var packet in run(context, token).WithCancellation(token))
                    await context.WriteAsync(Output, packet.Value, token, packet.Reference);
            }
        );

    public static void Transform<TInput, TOutput>(
        NodeRegistry registry,
        string type,
        string module,
        Func<DataPacket<TInput>, NodeContext, CancellationToken, IAsyncEnumerable<DataPacket<TOutput>>> run
    )
        where TInput : notnull
        where TOutput : notnull =>
        registry.Register(
            new(type, module, [PortDescriptor.Stream<TInput>(Input)], [PortDescriptor.Stream<TOutput>(Output)]),
            async (context, token) =>
            {
                await foreach (var input in context.ReadEventsAsync())
                {
                    var packet = (DataPacket<TInput>)((StreamInput)input).Packet;
                    await foreach (var result in run(packet, context, token).WithCancellation(token))
                        await context.WriteAsync(Output, result.Value, token, result.Reference);
                }
            }
        );

    public static void Sink<T>(
        NodeRegistry registry,
        string type,
        string module,
        Func<DataPacket<T>, NodeContext, CancellationToken, ValueTask> run
    )
        where T : notnull =>
        registry.Register(new(type, module, [PortDescriptor.Stream<T>(Input)], []), async (context, token) =>
        {
            await foreach (var input in context.ReadEventsAsync())
                await run((DataPacket<T>)((StreamInput)input).Packet, context, token);
        });
}
