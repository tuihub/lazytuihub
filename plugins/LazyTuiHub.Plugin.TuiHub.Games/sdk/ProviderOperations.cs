using LazyTuiHub.Interaction;
using LazyTuiHub.Plugins;

namespace LazyTuiHub.Games;

internal sealed class ProviderOperations(string provider, IWorkflowExecutor workflows)
{
    [Capability("Search this provider by title.")]
    public Task<GameMetadata[]> Search(string query, CancellationToken token) =>
        Execute<GameMetadata[]>("search", new(query), token);

    [Capability("Retrieve the provider's metadata for one source ID.")]
    public Task<GameMetadata> Lookup(string sourceId, CancellationToken token) =>
        Execute<GameMetadata>("lookup", new(sourceId), token);

    [Capability("Import a catalog entry into the library.")]
    public Task<GameEntry> Import(string sourceId, CancellationToken token, string? game = null) =>
        Execute<GameEntry>("import", new(sourceId, game), token);

    private async Task<T> Execute<T>(string action, GameRequest input, CancellationToken token)
        where T : class => (await workflows.CollectAsync<T>(provider + "." + action, input, token)).Single();
}
