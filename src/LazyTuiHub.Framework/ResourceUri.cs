namespace LazyTuiHub.Framework;

public static class ResourceUri
{
    private const string Prefix = "lazytuihub://";

    public static string InAccount(string plugin, string account, params string[] path) =>
        Create([plugin, "accounts", account, .. path]);

    public static string DeviceJobs(string device) => Create("core", "devices", device, "jobs");

    public static string AccountGrants(string account) => Create("core", "accounts", account, "grants");

    public static string AccountRelations(string account) => Create("core", "accounts", account, "relations");

    public static string DeviceGrants(string device) => Create("core", "devices", device, "grants");

    public static string SystemControl(string name) => Create("core", "system", name);

    public static bool IsAccountId(string value) =>
        Guid.TryParseExact(value, "N", out var id) && string.Equals(id.ToString("N"), value, StringComparison.Ordinal);

    public static string Create(params string[] path) => Prefix + string.Join('/', path.Select(Encode));

    public static string Canonical(string uri)
    {
        var segments = Segments(uri);
        var canonical = Create(segments);
        if (!string.Equals(canonical, uri, StringComparison.Ordinal))
            throw new ArgumentException("Provide a canonical resource URI.", nameof(uri));
        return canonical;
    }

    public static bool Covers(string scope, string target) =>
        string.Equals(scope, target, StringComparison.Ordinal)
        || target.StartsWith(scope + "/", StringComparison.Ordinal);

    public static string[] Segments(string uri)
    {
        if (!uri.StartsWith(Prefix, StringComparison.Ordinal))
            throw new ArgumentException("Provide a LazyTuiHub resource URI.", nameof(uri));
        return uri[Prefix.Length..].Split('/').Select(Uri.UnescapeDataString).ToArray();
    }

    private static string Encode(string segment) =>
        segment is "" or "." or ".." || segment.Any(char.IsControl)
            ? throw new ArgumentException("Provide a nonempty resource identifier.", nameof(segment))
            : Uri.EscapeDataString(segment);
}
