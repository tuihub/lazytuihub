using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace LazyTuiHub.Framework;

public sealed class PipelineExecutionException(string nodeId, Exception innerException)
    : Exception($"Pipeline node '{nodeId}' failed: {innerException.Message}", innerException)
{
    public string NodeId { get; } = nodeId;
}
