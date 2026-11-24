namespace LazyTuiHub.Games;

public sealed record GameOrigin(
    string Id,
    string Provider,
    string ExternalId,
    string Label,
    bool Local,
    bool Connected,
    string? LastSync,
    long Revision
)
{
    public string? CurrentDevicePath =>
        Local && ExternalId.StartsWith(Environment.MachineName + "/", StringComparison.Ordinal)
            ? ExternalId[(Environment.MachineName.Length + 1)..]
            : null;
    public string Description
    {
        get
        {
            var synced = DateTimeOffset.TryParse(
                LastSync,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var date
            )
                ? "Last sync " + date.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture)
                : "Never synced";
            return (Connected ? string.Empty : "Disconnected · ") + synced;
        }
    }
}
