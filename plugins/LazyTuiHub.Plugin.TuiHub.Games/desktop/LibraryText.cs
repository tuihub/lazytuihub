using System.Text.RegularExpressions;

namespace LazyTuiHub.Games.Desktop;

internal static partial class LibraryText
{
    public static string Name(string id) =>
        id switch
        {
            "steam" => "Steam",
            "bangumi" => "Bangumi",
            "vndb" => "VNDB",
            "folder" => "Game folders",
            _ => id,
        };

    public static string Description(GameField? description)
    {
        if (description is null)
            return string.Empty;
        var text = description.Source.StartsWith("vndb:", StringComparison.Ordinal)
            ? Formatting().Replace(description.Value, string.Empty)
            : description.Value;
        if (description.Source is "Personal")
            return text;
        return string.Join(
            '\n',
            text.ReplaceLineEndings("\n")
                .Split('\n')
                .Select(line =>
                {
                    var trimmed = line.AsSpan().Trim();
                    return trimmed.Length >= 4 && trimmed[0] is '=' or '-' && trimmed.IndexOfAnyExcept(trimmed[0]) < 0
                        ? string.Empty
                        : line;
                })
        );
    }

    [GeneratedRegex(
        @"\[/?(?:url(?:=[^\]\r\n]+)?|i|b|u)\]",
        RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant
            | RegexOptions.NonBacktracking
            | RegexOptions.ExplicitCapture
    )]
    private static partial Regex Formatting();
}
