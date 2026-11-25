namespace LazyTuiHub.Sdk;

public interface IRetentionCleanup
{
    void Clean(DateTimeOffset now, CancellationToken token);
}
