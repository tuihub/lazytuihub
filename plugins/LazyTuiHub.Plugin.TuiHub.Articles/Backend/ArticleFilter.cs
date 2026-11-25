using System.Text.Json;
using System.Text.RegularExpressions;
using Grpc.Core;
using LazyTuiHub.Api;

namespace LazyTuiHub.Plugin.TuiHub.Articles;

internal static partial class ArticleFilter
{
    private const string Source = """source\s*=\s*(?<source>"(?:[^"\\]|\\.)*")""";
    private const string Read = @"read\s*=\s*(?<read>true|false)";
    private const string And = @"\s+AND\s+";

    public static (string? Source, bool? Read) Parse(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return (null, null);
        var match = Expression().Match(filter);
        if (match.Success)
        {
            try
            {
                var source = match.Groups["source"];
                var read = match.Groups["read"];
                return (
                    source.Success ? JsonSerializer.Deserialize<string>(source.Value) : null,
                    read.Success ? read.Value is "true" : null
                );
            }
            catch (JsonException)
            {
                throw InvalidFilter();
            }
        }
        throw InvalidFilter();
    }

    [GeneratedRegex(
        @"^\s*(?:" + Source + "(?:" + And + Read + ")?|" + Read + "(?:" + And + Source + @")?)\s*\z",
        RegexOptions.NonBacktracking
    )]
    private static partial Regex Expression();

    private static RpcException InvalidFilter() =>
        RpcErrors.Create(
            StatusCode.InvalidArgument,
            "INVALID_FILTER",
            "Use equality on source or read, optionally combined with AND."
        );
}
