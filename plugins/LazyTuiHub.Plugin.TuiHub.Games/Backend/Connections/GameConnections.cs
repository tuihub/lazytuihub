using LazyTuiHub.Framework;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub.Plugin.TuiHub.Games;

internal sealed class GameConnections(
    GameLibrary library,
    IEnumerable<GameAccountProvider> platforms,
    HttpClient http,
    IDataProtectionProvider protection,
    ResourceAccess access,
    IEnumerable<IInstallationScanner> scanners
)
{
    public IReadOnlyList<GameAccountProvider> Providers { get; } = platforms.ToArray();
    public IReadOnlyList<IInstallationScanner> Scanners { get; } = scanners.ToArray();

    public async Task<GameOrigin> BindAsync(
        string provider,
        string tokenText,
        Action<Uri> showBrowser,
        CancellationToken token
    )
    {
        library.RequireOrigins(AccessMode.Read | AccessMode.Write);
        var service = Provider(provider);
        var identity = await service.IdentifyAsync(http, tokenText, showBrowser, token);
        var secret = service.UsesToken ? Protector(provider, identity.Id).Protect(tokenText) : string.Empty;
        return library.Connect(provider, identity, false, secret);
    }

    public async Task<int> SyncAsync(string connection, CancellationToken token)
    {
        var origin = library.Origins().Single(item => string.Equals(item.Id, connection, StringComparison.Ordinal));
        var encrypted = library.Credential(origin);
        var service = Provider(origin.Provider);
        var secret = service.UsesToken
            ? Protector(origin.Provider, origin.ExternalId).Unprotect(encrypted)
            : string.Empty;
        var games = await service.LibraryAsync(http, origin.ExternalId, secret, token);
        return await Task.Run(() => library.ReplaceInventory(origin, games, token), token);
    }

    public async Task<int> ScanAsync(string scanner, string root, CancellationToken token)
    {
        library.RequireOrigins(AccessMode.Read | AccessMode.Write);
        var service = Scanners.Single(s => s.Id.Equals(scanner, StringComparison.Ordinal));
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var key = Environment.MachineName + "/" + path;
        var origin = library.Connect(service.Id, new(key, Environment.MachineName + " · " + path), true, string.Empty);
        return await Task.Run(() => library.ReplaceInventory(origin, service.Scan(path, token), token), token);
    }

    private IDataProtector Protector(string provider, string id) =>
        protection.CreateProtector("LazyTuiHub.Games.Bindings", access.Account, provider, id);

    private GameAccountProvider Provider(string id) =>
        Providers.FirstOrDefault(p => p.Id.Equals(id, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Enable the {id} plugin to access this account.");
}
