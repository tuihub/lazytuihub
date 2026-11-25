namespace LazyTuiHub.Plugins;

public interface IRetentionCleanup
{
    void Clean(DateTimeOffset now, CancellationToken token);
}
