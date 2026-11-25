using LazyTuiHub.Framework;

namespace LazyTuiHub.Sdk;

public interface IWorkflowExecutor
{
    Task RunAsync(string name, PipelineContext? context = null, CancellationToken cancellationToken = default);
}
