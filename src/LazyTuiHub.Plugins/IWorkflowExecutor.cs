using LazyTuiHub.Framework;

namespace LazyTuiHub.Plugins;

public interface IWorkflowExecutor
{
    Task RunAsync(string name, PipelineContext? context = null, CancellationToken cancellationToken = default);
}
