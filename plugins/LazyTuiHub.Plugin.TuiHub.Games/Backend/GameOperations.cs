using System.Text.Json;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed class GameOperations(GameLibrary library, GameConnections accounts, GameCuration curation)
{
    public bool CanAdd => library.CanAddGames;
    public bool CanViewSources => library.CanReadConnections;
    public bool CanManageSources => library.CanManageConnections;

    public bool CanEdit(string game) => library.CanEditGame(game);

    public IReadOnlyList<GameAccountProvider> Platforms => accounts.Providers;
    public IReadOnlyList<IInstallationScanner> InstallationScanners => accounts.Scanners;

    [Capability("List installed providers and installation scanners.")]
    public object Providers() =>
        new
        {
            Platforms = Platforms.Select(p => new { p.Id, p.UsesToken }),
            Scanners = InstallationScanners.Select(s => s.Id),
        };

    [Capability("List game entries.")]
    public IReadOnlyList<GameEntry> List() => library.List();

    [Capability(
        "Browse the library by title, alias, category, provider or scope (All, Installed, Online, Needs match)."
    )]
    public IReadOnlyList<GameShelfItem> Shelf(
        string query = "",
        string? category = null,
        string? provider = null,
        string scope = "All"
    ) => FilterShelf(library.Shelf(), query, category, provider, scope).ToArray();

    public static IEnumerable<GameShelfItem> FilterShelf(
        IEnumerable<GameShelfItem> shelf,
        string query,
        string? category,
        string? provider,
        string scope
    ) =>
        shelf.Where(i =>
            (
                i.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || i.Aliases.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase))
            )
            && (category is null || i.Categories.Contains(category, StringComparer.Ordinal))
            && (provider is null || i.Providers.Contains(provider, StringComparer.Ordinal))
            && InScope(i, scope)
        );

    private static bool InScope(GameShelfItem i, string scope) =>
        scope switch
        {
            "Installed" => i.Installations > 0,
            "Online" => i.Accounts > 0,
            "Needs match" => i.NeedsMatch,
            "All" => true,
            _ => throw new ArgumentException($"Unknown shelf scope '{scope}'.", nameof(scope)),
        };

    [Capability("Inspect account and folder connections.")]
    public IReadOnlyList<GameOrigin> Connections() => library.Origins();

    [Capability("Connect a platform account. Token-based providers prompt for a personal token.")]
    public Task<GameOrigin> Bind(
        string provider,
        SecretInput personalToken,
        InteractionOutput output,
        CancellationToken token
    )
    {
        var platform = Platforms.FirstOrDefault(p => p.Id.Equals(provider, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown provider '{provider}'.", nameof(provider));
        return accounts.BindAsync(
            provider,
            platform.UsesToken ? personalToken.Read() : string.Empty,
            uri => output.Notify(uri.AbsoluteUri),
            token
        );
    }

    [Capability("Synchronize a connected account.")]
    public Task<int> Sync(string connection, CancellationToken token) => accounts.SyncAsync(connection, token);

    [Capability("Disconnect a source and keep its library snapshot.")]
    public void Disconnect(string connection) => library.Disconnect(connection);

    [Capability("Scan a local game folder or Steam library.")]
    public Task<int> Scan(string scanner, string root, CancellationToken token) =>
        accounts.ScanAsync(scanner, root, token);

    [Capability("Fill missing metadata from installed catalogs.")]
    public Task<int> Fill(string game, CancellationToken token) => curation.FillAsync(game, token);

    [Capability("Find metadata candidates for a game.")]
    public Task<CatalogKey[]> Candidates(string game, CancellationToken token) => curation.CandidatesAsync(game, token);

    [Capability("Search a provider catalog by title.")]
    public Task<GameMetadata[]> Search(string provider, string query, CancellationToken token) =>
        curation.SearchAsync(provider, query, token);

    [Capability("Link or refresh a catalog entry.")]
    public Task Link(string game, string provider, string sourceId, CancellationToken token) =>
        curation.LinkAsync(game, new(provider, sourceId), token);

    [Capability("Remove a linked catalog entry.")]
    public void Unlink(string game, string provider, string sourceId) => library.Unlink(game, provider, sourceId);

    [Capability("Override a metadata field. Omit value to restore the source.")]
    public void SetField(string game, string field, string? value = null) => library.SetField(game, field, value);

    [Capability("Move a source observation to an existing game.")]
    public void Assign(string connection, string evidence, string game) => library.Assign(connection, evidence, game);

    [Capability("Move a source observation to a new game.")]
    public GameEntry Split(string connection, string evidence, string title) =>
        library.Split(connection, evidence, title);

    [Capability("Create a game.")]
    public GameEntry Add(string title) => library.Add(title);

    [Capability("Show game details, linked sources and sessions.")]
    public GameDetails Show(string game) => library.Show(game);

    [Capability("Change the title or add or remove a category.")]
    public GameEntry Edit(
        string game,
        string? title = null,
        string? addCategory = null,
        string? removeCategory = null
    ) => library.Edit(game, title, addCategory, removeCategory);

    [Capability("Save the title, categories (JSON array) and field overrides (JSON object) together.")]
    public void EditInfo(string game, string title, string[] categories, IReadOnlyDictionary<string, string?> fields) =>
        library.EditInfo(new(game, title, categories), fields);

    [Capability("Record a play session with an ISO start and a duration.")]
    public void LogSession(string game, string device, DateTimeOffset start, TimeSpan duration) =>
        library.LogSession(game, new(device, start, duration));

    [Capability("Find matching source IDs in a dataset file.")]
    public static async Task<JsonElement[]> Matches(
        string dataset,
        string provider,
        string sourceId,
        CancellationToken token
    )
    {
        await using var input = File.OpenRead(dataset);
        return await GameIdDataset.FindAsync(input, provider, sourceId, token);
    }
}
