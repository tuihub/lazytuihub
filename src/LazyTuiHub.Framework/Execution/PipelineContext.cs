namespace LazyTuiHub.Framework;

public sealed record PipelineContext(
    object? Input = null,
    Func<DataPacket, CancellationToken, ValueTask>? Output = null,
    bool Continuous = false,
    Action<NodeProgress>? Progress = null
);
