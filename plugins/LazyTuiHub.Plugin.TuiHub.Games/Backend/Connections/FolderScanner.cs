namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed class FolderScanner : IInstallationScanner
{
    public string Id => "folder";

    public IReadOnlyList<PlatformGame> Scan(string root, CancellationToken token)
    {
        var result = new List<PlatformGame>();
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            token.ThrowIfCancellationRequested();
            var name = Path.GetFileName(folder);
            result.Add(new(folder, name, "Installed", folder));
        }
        return result;
    }
}
