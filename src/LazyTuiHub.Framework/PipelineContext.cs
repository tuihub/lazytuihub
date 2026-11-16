using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed record PipelineContext(
    object? Input = null,
    Func<DataPacket, CancellationToken, ValueTask>? Output = null,
    bool Continuous = false
);
