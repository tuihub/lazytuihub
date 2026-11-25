namespace LazyTuiHub.Plugin.TuiHub.Games;

public interface IInstallationScanner
{
    string Id { get; }
    IReadOnlyList<PlatformGame> Scan(string root, CancellationToken token);
}
