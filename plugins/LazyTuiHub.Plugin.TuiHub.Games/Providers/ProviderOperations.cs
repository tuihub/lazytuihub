using LazyTuiHub.Interaction;
using LazyTuiHub.Sdk;

namespace LazyTuiHub.Plugin.TuiHub.Games;

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
        where T : class
    {
        var workflow = provider + "." + action;
        var results = await workflows.CollectAsync<T>(workflow, input, token);
        return results.Count == 1
            ? results[0]
            : throw new InvalidOperationException(
                $"Workflow '{workflow}' must return one {typeof(T).Name} result, received {results.Count}."
            );
    }
}
