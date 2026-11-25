using Avalonia.Data.Converters;

namespace LazyTuiHub.Plugin.TuiHub.Games.Desktop;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0182",
    Justification = "Referenced in compiled Avalonia XAML."
)]
internal static class GameNames
{
    public static FuncValueConverter<string, string> Scanner { get; } =
        new(id =>
            id switch
            {
                "steam" => "Steam library",
                "folder" => "Game folder",
                _ => id ?? string.Empty,
            }
        );
    public static FuncValueConverter<string, string> ScannerHelp { get; } =
        new(id =>
            id switch
            {
                "steam" => "Read installed games from Steam manifests.",
                "folder" => "Import each immediate subfolder as a game.",
                _ => string.Empty,
            }
        );
    public static FuncValueConverter<string, string> Platform { get; } =
        new(id => LibraryText.Name(id ?? string.Empty));
}
