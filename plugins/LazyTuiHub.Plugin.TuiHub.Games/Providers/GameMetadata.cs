namespace LazyTuiHub.Plugin.TuiHub.Games;

public sealed record GameMetadata(
    string Provider,
    string ExternalId,
    string Title,
    string Url,
    string? Description = null,
    string? ImageUrl = null,
    string? ReleaseDate = null,
    string? AlternativeTitle = null,
    string[]? Developers = null,
    string[]? Publishers = null,
    string? SourceKind = null,
    string[]? OtherTitles = null
);
