using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games;

public abstract class GameProviderPlugin(string provider) : IApplicationPlugin
{
    public const string GameLibraryId = "lazytuihub.plugin.tuihub.games";
    protected string Provider { get; } = provider;
    private const string Search = "search";
    private const string Lookup = "lookup";

    protected abstract Task<GameMetadata[]> SearchAsync(HttpClient client, string query, CancellationToken token);
    protected abstract Task<GameMetadata> LookupAsync(HttpClient client, string id, CancellationToken token);

    public virtual void Configure(PluginRegistration registration)
    {
        registration.DataTypes.Add(Provider + ".game");
        registration.RegisterNodes = (nodes, services) =>
        {
            var http = services.GetRequiredService<HttpClient>();
            Register<GameMetadata[]>(nodes, Provider, Search, (query, token) => SearchAsync(http, query, token));
            Register<GameMetadata>(nodes, Provider, Lookup, (key, token) => LookupAsync(http, key, token));
        };
        foreach (var action in new[] { Search, Lookup, "import" })
            registration.Workflows.Add(Workflow(Provider, registration.Id, action));
        registration.AddCapabilities(Provider, services => new ProviderOperations(
            Provider,
            services.GetRequiredService<IWorkflowExecutor>()
        ));
    }

    private static void Register<T>(
        NodeRegistry nodes,
        string id,
        string action,
        Func<string, CancellationToken, Task<T>> execute
    )
        where T : notnull =>
        nodes.RegisterRepeatable<string, T>(
            new(
                id + "." + action,
                id,
                [PortDescriptor.Stream<string>(StreamNodes.Input)],
                [PortDescriptor.Stream<T>(StreamNodes.Output)]
            ),
            (input, _, token) => execute(input, token)
        );

    private static PipelineDefinition Workflow(string provider, string pluginId, string action)
    {
        var nodes = new List<PipelineNode>
        {
            PipelineNode.Create("query", "games.query", pluginId: GameLibraryId),
            PipelineNode.Create("provider", provider + "." + (action is Search ? Search : Lookup), pluginId: pluginId),
        };
        if (action is "import")
        {
            nodes.Add(PipelineNode.Create("prepare", "games.prepare-import", pluginId: GameLibraryId));
            nodes.Add(PipelineNode.Create("store", "games.store", pluginId: GameLibraryId));
        }
        nodes.Add(PipelineNode.Create("output", $"games.{action}-output", pluginId: GameLibraryId));
        var edges = nodes
            .Zip(nodes.Skip(1), (a, b) => new PipelineEdge(a.Id, StreamNodes.Output, b.Id, StreamNodes.Input))
            .ToArray();
        return new(provider + "." + action, nodes, edges);
    }

    protected static async Task<JsonElement> RequestAsync(
        HttpClient client,
        Uri uri,
        object? body,
        CancellationToken token
    )
    {
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, uri);
        request.Headers.UserAgent.ParseAdd("TuiHub/LazyTuiHub (+https://github.com/tuihub/lazytuihub)");
        if (body is not null)
            request.Content = JsonContent.Create(body);
        try
        {
            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>(token);
        }
        catch (HttpRequestException error)
            when (error.StatusCode
                    is null
                        or HttpStatusCode.TooManyRequests
                        or HttpStatusCode.RequestTimeout
                        or >= HttpStatusCode.InternalServerError
            )
        {
            throw new InstanceUnavailableException($"{uri.Host}: {error.Message}", error);
        }
        catch (HttpRequestException error)
        {
            throw new PacketRejectedException($"{uri.Host}: {error.Message}", error);
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested)
        {
            throw new InstanceUnavailableException($"Request to {uri.Host} timed out.", error);
        }
    }

    protected static string? Text(JsonElement row, string field) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(field, out var value) ? value.GetString() : null;

    protected static string NumericId(string id) =>
        id.Length > 0 && id[0] != '0' && id.All(char.IsAsciiDigit)
            ? id
            : throw new ArgumentException("Provide a positive decimal source ID.", nameof(id));
}
