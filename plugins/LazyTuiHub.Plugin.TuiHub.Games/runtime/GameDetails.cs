namespace LazyTuiHub.Games;

public sealed record GameDetails(
    GameEntry Entry,
    IReadOnlyList<GameMetadata> Sources,
    IReadOnlyList<GameSession> Sessions,
    IReadOnlyList<GameAvailability> Availability,
    IReadOnlyDictionary<string, string> Overrides
)
{
    public IReadOnlyList<GameField> Fields
    {
        get
        {
            List<GameField> fields = [];
            void Select(string name, Func<GameMetadata, string?> read)
            {
                if (Overrides.TryGetValue(name, out var personal))
                {
                    fields.Add(new(name, personal, "Personal"));
                    return;
                }
                foreach (var source in Sources)
                {
                    var value = read(source);
                    if (string.IsNullOrWhiteSpace(value))
                        continue;
                    fields.Add(new(name, value, source.Provider + ":" + source.ExternalId));
                    break;
                }
            }
            Select("Description", s => s.Description);
            Select("Cover", s => s.ImageUrl);
            Select("Released", s => s.ReleaseDate);
            Select("Other title", s => s.AlternativeTitle ?? string.Join(", ", s.OtherTitles ?? []));
            Select("Developers", s => string.Join(", ", s.Developers ?? []));
            Select("Publishers", s => string.Join(", ", s.Publishers ?? []));
            return fields;
        }
    }
}
