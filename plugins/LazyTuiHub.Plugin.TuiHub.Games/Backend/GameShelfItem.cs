namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed record GameShelfItem(string Id, string Title, int Accounts, int Installations, string? Cover = null)
{
    public string[] Aliases { get; init; } = [];
    public string[] Categories { get; init; } = [];
    public string[] Providers { get; init; } = [];
    public string? Released { get; init; }
    public string? Developers { get; init; }
    public bool NeedsMatch => Providers.Length == 0;
    public string Summary =>
        (Accounts, Installations) switch
        {
            (> 0, > 0) => "Online library · Installed",
            (> 0, _) => "Online library",
            (_, > 0) => "Installed",
            _ => "Personal library",
        };
}
