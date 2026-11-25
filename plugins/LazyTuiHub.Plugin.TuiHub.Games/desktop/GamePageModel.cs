using System.Collections.ObjectModel;
using LazyTuiHub.Desktop;
using LazyTuiHub.Interaction;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Games.Desktop;

public sealed class GamePageModel(GameOperations games, DesktopSession session, ILogger<GamePageModel> logger)
    : PageModel(session, logger)
{
    public GameScreen Screen { get; private set; }
    public GameDetails? Selected { get; private set; }
    public bool InLibrary => Screen == GameScreen.Library;
    public bool InDetail => Screen == GameScreen.Detail;
    public bool InSources => Screen == GameScreen.Sources;
    public bool InAdd => Screen == GameScreen.Add;
    public bool Identifying => Screen == GameScreen.Identify;
    public bool Editing => Screen == GameScreen.Edit;
    public bool Moving => Screen == GameScreen.Reassign;
    public bool Connecting => Screen == GameScreen.Connect;
    public bool Scanning => Screen == GameScreen.Scan;
    public bool NeedsSheet => !InLibrary && !InDetail && !InSources;
    public bool CanAdd => games.CanAdd;
    public bool CanViewSources => games.CanViewSources;
    public bool CanScan => CanImport && Scanners.Count > 0;
    public bool CanManage => games.CanManageSources;
    public bool CanEdit => Selected is { } game && games.CanEdit(game.Entry.Id);
    public bool CanMove => CanEdit && CanManage;
    public bool CanImport => CanManage && CanAdd;
    public bool Empty => All.Count == 0;
    public bool ShowPosters => Shelf.Count > 0 && !CompactList;
    public bool ShowRows => Shelf.Count > 0 && CompactList;
    public bool NoMatches => !Empty && Shelf.Count == 0;
    public bool HasExtraFilters => Category is not null || SourceFilter is not null;
    public bool HasNotice => !Busy && !string.IsNullOrWhiteSpace(Status);
    public string ViewAction => CompactList ? "Poster view" : "List view";
    public bool CompactList { get; private set; }
    public IReadOnlyList<GameShelfItem> All { get; private set; } = [];
    public IReadOnlyList<GameCard> Shelf { get; private set; } = [];
    public string Search { get; set; } = string.Empty;
    public string Filter { get; private set; } = "All";
    public string? Category { get; set; }
    public string? SourceFilter { get; set; }
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public IReadOnlyList<string> SourceNames { get; private set; } = [];
    public string Count => Shelf.Count == 1 ? "1 game" : $"{Shelf.Count} games";
    public IReadOnlyList<GamePlatformGroup> Platforms { get; private set; } = [];
    public IReadOnlyList<GameOriginRow> Folders { get; private set; } = [];
    public IReadOnlyList<GameAccountProvider> Providers => games.Platforms;
    public IReadOnlyList<IInstallationScanner> Scanners => games.InstallationScanners;
    public GameAccountProvider? Platform { get; private set; }
    public IInstallationScanner? Scanner { get; private set; }
    public string FolderPath { get; set; } = string.Empty;
    public GameAvailability? Observation { get; private set; }
    public string ObservationName =>
        Observation is { } item ? $"{LibraryText.Name(item.Provider)} · {item.Label} · {item.Status}" : string.Empty;
    public string? Cover => Field("Cover")?.Value;
    public string? Alias => Field("Other title")?.Value;
    public string? Released => Field("Released")?.Value;
    public string? Developers => Field("Developers")?.Value;
    public string? Publishers => Field("Publishers")?.Value;
    public string Description => LibraryText.Description(Field("Description"));
    public IEnumerable<GameAvailability>? Installs => Selected?.Availability.Where(item => item.Location is not null);
    public IEnumerable<GameAvailability>? Collections => Selected?.Availability.Where(item => item.Location is null);
    public bool HasInstalls => Installs?.Any() == true;
    public bool HasCollections => Collections?.Any() == true;
    public bool HasSessions => Selected?.Sessions.Count > 0;
    public bool HasSources => Selected?.Sources.Count > 0;
    public string SheetTitle =>
        Screen switch
        {
            GameScreen.Add => "Add game",
            GameScreen.Connect => "Connect " + LibraryText.Name(Platform!.Id),
            GameScreen.Scan => Scanner?.Id is "steam" ? "Scan a Steam library" : "Scan a game folder",
            GameScreen.Edit => "Edit game info",
            GameScreen.Identify => "Identify game",
            GameScreen.Reassign => "Reassign entry",
            _ => string.Empty,
        };
    public string DraftTitle { get; set; } = string.Empty;
    public ObservableCollection<string> DraftCategories { get; } = [];
    public IReadOnlyList<GameFieldDraft> DraftFields { get; private set; } = [];
    public IReadOnlyList<CatalogKey> Matches { get; private set; } = [];
    public IReadOnlyList<GameMetadata> Results { get; private set; } = [];
    public IReadOnlyList<GameShelfItem> Targets { get; private set; } = [];
    private bool loaded;
    private static readonly string[] Editable =
    [
        "Released",
        "Developers",
        "Publishers",
        "Other title",
        "Cover",
        "Description",
    ];

    public Task LoadAsync() => loaded ? Task.CompletedTask : ReloadAsync();

    public Task ReloadAsync() =>
        RunAsync(_ =>
        {
            Refresh();
            loaded = true;
            Status = string.Empty;
            return Task.CompletedTask;
        });

    private GameField? Field(string name) =>
        Selected?.Fields.FirstOrDefault(item => item.Name.Equals(name, StringComparison.Ordinal));

    private void Refresh(string? id = null)
    {
        All = games.Shelf();
        var origins = games.CanViewSources ? games.Connections() : [];
        Platforms = Providers
            .Select(p => p.Id)
            .Concat(origins.Where(o => !o.Local).Select(o => o.Provider))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(key => new GamePlatformGroup(
                LibraryText.Name(key),
                Providers.FirstOrDefault(p => p.Id.Equals(key, StringComparison.Ordinal)),
                origins
                    .Where(o => !o.Local && o.Provider.Equals(key, StringComparison.Ordinal))
                    .Select(o => new GameOriginRow(
                        o,
                        CanImport && o.Connected && Providers.Any(p => p.Id.Equals(key, StringComparison.Ordinal)),
                        CanManage && o.Connected
                    ))
                    .ToArray(),
                CanManage
            ))
            .ToArray();
        Folders = origins
            .Where(o => o.Local)
            .Select(o => new GameOriginRow(
                o,
                CanImport
                    && o.CurrentDevicePath is not null
                    && Scanners.Any(scanner => scanner.Id.Equals(o.Provider, StringComparison.Ordinal)),
                CanManage && o.Connected
            ))
            .ToArray();
        Categories = All.SelectMany(i => i.Categories)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        SourceNames = All.SelectMany(i => i.Providers)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (Category is not null && !Categories.Contains(Category, StringComparer.Ordinal))
            Category = null;
        if (SourceFilter is not null && !SourceNames.Contains(SourceFilter, StringComparer.Ordinal))
            SourceFilter = null;
        if ((id ?? Selected?.Entry.Id) is { } selected)
            Selected = games.Show(selected);
        ApplyFilters();
    }

    public void ApplyFilters()
    {
        Shelf = GameOperations
            .FilterShelf(All, Search, Category, SourceFilter, Filter)
            .Select(i => new GameCard(i, string.Equals(i.Id, Selected?.Entry.Id, StringComparison.Ordinal)))
            .ToArray();
        Changed();
    }

    public void SetFilter(string value)
    {
        Filter = value;
        ApplyFilters();
    }

    public void SetList(bool value)
    {
        CompactList = value;
        Changed();
    }

    public void Go(GameScreen screen)
    {
        Screen = screen;
        Status = string.Empty;
        Changed();
    }

    public void Back() =>
        Go(
            Screen switch
            {
                GameScreen.Connect or GameScreen.Scan => GameScreen.Sources,
                GameScreen.Identify or GameScreen.Edit or GameScreen.Reassign => GameScreen.Detail,
                _ => GameScreen.Library,
            }
        );

    public Task SelectAsync(string id) =>
        RunAsync(_ =>
        {
            Selected = games.Show(id);
            foreach (var card in Shelf)
                card.Select(string.Equals(card.Game.Id, id, StringComparison.Ordinal));
            Go(GameScreen.Detail);
            return Task.CompletedTask;
        });

    public void StartAdd()
    {
        DraftTitle = string.Empty;
        DraftCategories.Clear();
        Go(GameScreen.Add);
    }

    public Task AddAsync() =>
        RunAsync(_ =>
        {
            var game = games.Add(DraftTitle);
            Refresh(game.Id);
            Go(GameScreen.Detail);
            return Task.CompletedTask;
        });

    public void StartEdit()
    {
        var game = Selected!;
        DraftTitle = game.Entry.Title;
        DraftCategories.Clear();
        foreach (var category in game.Entry.Categories)
            DraftCategories.Add(category);
        var upstream = (game with { Overrides = new Dictionary<string, string>(StringComparer.Ordinal) }).Fields;
        DraftFields = Editable
            .Select(name => new GameFieldDraft(
                name,
                Field(name)?.Value,
                upstream.FirstOrDefault(f => f.Name.Equals(name, StringComparison.Ordinal))?.Value,
                Field(name)?.Source,
                game.Overrides.ContainsKey(name)
            ))
            .ToArray();
        Go(GameScreen.Edit);
    }

    public Task SaveAsync() =>
        RunAsync(_ =>
        {
            var game = Selected!.Entry;
            games.EditInfo(
                game.Id,
                DraftTitle,
                DraftCategories.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                DraftFields.Where(f => f.Changed).ToDictionary(f => f.Name, f => f.Override, StringComparer.Ordinal)
            );
            Refresh(game.Id);
            Go(GameScreen.Detail);
            Status = "Changes saved.";
            return Task.CompletedTask;
        });

    private async Task SuggestionsAsync(CancellationToken token)
    {
        var game = Selected!;
        Matches = (await games.Candidates(game.Entry.Id, token))
            .Where(key =>
                Providers.Any(p => p.Id.Equals(key.Provider, StringComparison.Ordinal))
                && !game.Sources.Any(s =>
                    s.Provider.Equals(key.Provider, StringComparison.Ordinal)
                    && s.ExternalId.Equals(key.Id, StringComparison.Ordinal)
                )
            )
            .ToArray();
    }

    public Task IdentifyAsync() =>
        RunAsync(async ct =>
        {
            Results = [];
            Matches = [];
            Go(GameScreen.Identify);
            await SuggestionsAsync(ct);
            Status = string.Empty;
        });

    public Task SearchAsync(string provider, string query) =>
        RunAsync(async ct =>
        {
            Results = [];
            Results = await games.Search(provider, query, ct);
            Status = Results.Count == 0 ? "No catalog matches." : string.Empty;
        });

    public Task LinkAsync(CatalogKey key) =>
        RunAsync(async ct =>
        {
            var id = Selected!.Entry.Id;
            await games.Link(id, key.Provider, key.Id, ct);
            Refresh(id);
            await SuggestionsAsync(ct);
            Status = "Source linked or refreshed.";
        });

    public Task UnlinkAsync(GameMetadata source) =>
        RunAsync(async ct =>
        {
            var id = Selected!.Entry.Id;
            games.Unlink(id, source.Provider, source.ExternalId);
            Refresh(id);
            await SuggestionsAsync(ct);
            Status = "Source removed.";
        });

    public Task FillAsync() =>
        RunAsync(async ct =>
        {
            var id = Selected!.Entry.Id;
            var count = await games.Fill(id, ct);
            Refresh(id);
            Status =
                count == 0
                    ? "No new matches in the installed catalogs. Use Identify to search by title."
                    : $"Linked {count} sources.";
        });

    public void StartMove(GameAvailability observation)
    {
        Observation = observation;
        DraftTitle = Selected!.Entry.Title;
        FindTargets(string.Empty);
        Go(GameScreen.Reassign);
    }

    public void FindTargets(string query)
    {
        Targets = All.Where(i =>
                !string.Equals(i.Id, Selected?.Entry.Id, StringComparison.Ordinal)
                && games.CanEdit(i.Id)
                && i.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        Changed();
    }

    public Task MoveAsync(string? target) =>
        RunAsync(_ =>
        {
            var item = Observation!;
            var key = item.Location ?? item.SourceId;
            if (target is null)
                target = games.Split(item.Origin, key, DraftTitle).Id;
            else
                games.Assign(item.Origin, key, target);
            Refresh(target);
            Go(GameScreen.Detail);
            Status = "Entry moved to this game.";
            return Task.CompletedTask;
        });

    public void StartConnect(GameAccountProvider platform)
    {
        Platform = platform;
        Go(GameScreen.Connect);
    }

    public void StartScan(IInstallationScanner scanner, string path = "")
    {
        Scanner = scanner;
        FolderPath = path;
        Go(GameScreen.Scan);
    }

    public Task BindAsync(string secret, Action<Uri> browser, CancellationToken cancel) =>
        RunAsync(async ct =>
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct, cancel);
            try
            {
                var origin = await games.Bind(
                    Platform!.Id,
                    new SecretInput(() => secret),
                    new InteractionOutput(_ => { }, address => browser(new Uri(address))),
                    stop.Token
                );
                Refresh();
                Go(GameScreen.Sources);
                Status = $"Connected {origin.Label}. Choose Sync to import the collection.";
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                Status = "Sign-in cancelled.";
            }
        });

    public Task SyncAsync(string id) =>
        RunAsync(async ct =>
        {
            var count = await games.Sync(id, ct);
            Refresh();
            Status = $"Synchronized {count} games.";
        });

    public Task DisconnectAsync(string id) =>
        RunAsync(_ =>
        {
            games.Disconnect(id);
            Refresh();
            Status = "Disconnected. The last library snapshot is still available.";
            return Task.CompletedTask;
        });

    public Task ScanAsync() =>
        RunAsync(async ct =>
        {
            var count = await games.Scan(Scanner!.Id, FolderPath, ct);
            Refresh();
            Go(GameScreen.Sources);
            Status = $"Found {count} installations.";
        });
}
