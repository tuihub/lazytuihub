namespace LazyTuiHub.Plugin.TuiHub.Games;

public interface IGameCatalog
{
    Task<GameMetadata[]> SearchAsync(string query, CancellationToken token);
    Task<GameMetadata> LookupAsync(string id, CancellationToken token);
}
