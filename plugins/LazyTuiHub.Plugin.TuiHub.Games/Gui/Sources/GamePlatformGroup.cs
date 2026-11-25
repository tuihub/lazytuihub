namespace LazyTuiHub.Plugin.TuiHub.Games.Desktop;

internal sealed record GamePlatformGroup(
    string Name,
    GameAccountProvider? Provider,
    IReadOnlyList<GameOriginRow> Accounts,
    bool CanManage
)
{
    public bool Available => Provider is not null;
    public bool CanConnect => Available && CanManage;
    public string State =>
        (Available, Accounts.Count) switch
        {
            (false, _) => "Plugin unavailable",
            (true, 0) => "No accounts connected",
            _ => string.Empty,
        };
}
