namespace LazyTuiHub.Games;

public interface IInstallationScanner
{
    string Id { get; }
    IReadOnlyList<PlatformGame> Scan(string root, CancellationToken token);
}
