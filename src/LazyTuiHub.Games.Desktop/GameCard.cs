using System.ComponentModel;

namespace LazyTuiHub.Games.Desktop;

public sealed class GameCard(GameShelfItem game, bool selected) : INotifyPropertyChanged
{
    public GameShelfItem Game => game;
    public bool IsSelected { get; private set; } = selected;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Select(bool value)
    {
        if (IsSelected == value)
            return;
        IsSelected = value;
        PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
    }

    public string Status =>
        (Game.Installations > 0, Game.Accounts > 0) switch
        {
            (true, true) => "Installed · Online",
            (true, false) => "Installed",
            (false, true) => "Online",
            _ => Game.NeedsMatch ? "Needs match" : string.Empty,
        };
    public string Caption =>
        string.Join(
            " · ",
            new[] { Game.Released?.Split('-')[0], Game.Developers }.Where(s => !string.IsNullOrWhiteSpace(s))
        );
    public bool HasStatus => Status.Length > 0;
}
