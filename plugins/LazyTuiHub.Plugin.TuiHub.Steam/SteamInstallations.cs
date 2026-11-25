using System.Globalization;
using System.Text.RegularExpressions;
using LazyTuiHub.Games;

namespace LazyTuiHub.Steam;

internal sealed partial class SteamInstallations : IInstallationScanner
{
    public string Id => "steam";

    public IReadOnlyList<PlatformGame> Scan(string root, CancellationToken token)
    {
        var apps = Path.GetFileName(root).Equals("steamapps", StringComparison.OrdinalIgnoreCase)
            ? root
            : Path.Combine(root, "steamapps");
        var libraries = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
        )
        {
            apps,
        };
        var folders = Path.Combine(apps, "libraryfolders.vdf");
        if (File.Exists(folders))
            foreach (var (key, value) in Fields(folders))
                if (
                    key.StartsWith("libraryfolders/", StringComparison.OrdinalIgnoreCase)
                    && key.EndsWith("/path", StringComparison.OrdinalIgnoreCase)
                )
                    libraries.Add(Path.Combine(value, "steamapps"));
        var result = new List<PlatformGame>();
        foreach (var library in libraries)
        {
            foreach (var manifest in Directory.EnumerateFiles(library, "appmanifest_*.acf"))
            {
                token.ThrowIfCancellationRequested();
                var fields = Fields(manifest)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                var id = fields["AppState/appid"];
                var name = fields["AppState/name"];
                var folder = fields["AppState/installdir"];
                var flags = int.Parse(fields["AppState/StateFlags"], CultureInfo.InvariantCulture);
                const int fullyInstalled = 4;
                if (
                    (flags & fullyInstalled) == 0
                    || !string.Equals(folder, Path.GetFileName(folder), StringComparison.Ordinal)
                    || folder is "." or ".."
                )
                    continue;
                var path = Path.Combine(library, "common", folder);
                if (Directory.Exists(path))
                    result.Add(new(id, name, "Installed", path));
            }
        }
        return result;
    }

    private static IEnumerable<KeyValuePair<string, string>> Fields(string path)
    {
        var parents = new List<string>();
        string? pending = null;
        foreach (Match token in Tokens().Matches(File.ReadAllText(path)))
        {
            if (token.ValueSpan[0] == '/')
                continue;
            if (token.ValueSpan[0] == '{')
            {
                parents.Add(pending ?? throw new InvalidDataException("Invalid Steam manifest group."));
                pending = null;
            }
            else if (token.ValueSpan[0] == '}')
            {
                if (parents.Count == 0 || pending is not null)
                    throw new InvalidDataException("Incomplete Steam manifest.");
                parents.RemoveAt(parents.Count - 1);
            }
            else
            {
                var value = token
                    .Groups["value"]
                    .Value.Replace(@"\\", @"\", StringComparison.Ordinal)
                    .Replace("\\\"", "\"", StringComparison.Ordinal);
                if (pending is null)
                    pending = value;
                else
                {
                    yield return KeyValuePair.Create(string.Join('/', parents.Append(pending)), value);
                    pending = null;
                }
            }
        }
        if (parents.Count != 0 || pending is not null)
            throw new InvalidDataException("Incomplete Steam manifest.");
    }

    [GeneratedRegex(
        """
            //[^\r\n]*|[{}]|"(?<value>(?:\\.|[^"\\])*)"
            """,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture
    )]
    private static partial Regex Tokens();
}
