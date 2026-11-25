using LazyTuiHub.Framework;
using LazyTuiHub.Plugin.TuiHub.Rss;

namespace LazyTuiHub.Plugin.TuiHub.Rss.Application;

internal sealed partial class RssRefreshWorker(RssOperations workspace, ILogger<RssRefreshWorker> logger)
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await workspace.ExecuteAsync("rss.ingest", new PipelineContext(Continuous: true), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (PipelineExecutionException exception)
            {
                LogPipelineFailure(logger, exception.NodeId, exception);
            }
            await Task.Delay(workspace.RefreshInterval, stoppingToken);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "RSS pipeline failed at {NodeId}; retrying on the next refresh interval."
    )]
    private static partial void LogPipelineFailure(ILogger logger, string nodeId, Exception exception);
}
