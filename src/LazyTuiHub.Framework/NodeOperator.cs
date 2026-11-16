using System.Runtime.CompilerServices;

namespace LazyTuiHub.Framework;

internal delegate IAsyncEnumerable<DataPacket> NodeOperator(
    IAsyncEnumerable<DataPacket> input,
    PipelineNode node,
    PipelineContext context,
    CancellationToken cancellationToken
);
