using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games;

public abstract class GameProviderPlugin : IApplicationPlugin
{
    private const string Search = "search";
    private const string Lookup = "lookup";

    protected abstract Task<GameMetadata[]> SearchAsync(HttpClient client, string query, CancellationToken token);
    protected abstract Task<GameMetadata> LookupAsync(HttpClient client, string id, CancellationToken token);

    public virtual void Configure(PluginRegistration registration)
    {
        var id = registration.Id;
        var snapshot = registration.Configuration[$"Games:{id}:Snapshot"];
        registration.DataTypes.Add(id + ".game");
        registration.RegisterNodes = (nodes, services) =>
        {
            var http = services.GetRequiredService<HttpClient>();
            Register<GameMetadata[]>(nodes, id, Search, (query, token) =>
                snapshot is null
                    ? SearchAsync(http, query, token)
                    : GameSnapshot.SearchAsync(snapshot, id, query, token)
            );
            Register<GameMetadata>(nodes, id, Lookup, (key, token) =>
                snapshot is null ? LookupAsync(http, key, token) : GameSnapshot.LookupAsync(snapshot, id, key, token)
            );
        };
        foreach (var action in new[] { Search, Lookup, "import" })
            registration.Workflows.Add(Workflow(id, action));
        registration.Commands.Add(new(id, (app, action, token) => RunAsync(app, id, action, token)));
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

    private static PipelineDefinition Workflow(string provider, string action)
    {
        var nodes = new List<PipelineNode>
        {
            PipelineNode.Create("query", "games.query", pluginId: GamesPlugin.Id),
            PipelineNode.Create("provider", provider + "." + (action is Search ? Search : Lookup), pluginId: provider),
        };
        if (action is "import")
        {
            nodes.Add(PipelineNode.Create("prepare", "games.prepare-import", pluginId: GamesPlugin.Id));
            nodes.Add(PipelineNode.Create("store", "games.store", pluginId: GamesPlugin.Id));
        }
        nodes.Add(PipelineNode.Create("output", $"games.{action}-output", pluginId: GamesPlugin.Id));
        var edges = nodes
            .Zip(nodes.Skip(1), (a, b) => new PipelineEdge(a.Id, StreamNodes.Output, b.Id, StreamNodes.Input))
            .ToArray();
        return new(provider + "." + action, nodes, edges);
    }

    private static async Task RunAsync(WebApplication app, string id, string action, CancellationToken token)
    {
        if (action is "help")
        {
            Console.WriteLine($"{id}: search --query TEXT, lookup --source-id ID, import --source-id ID [--game ID].");
            return;
        }
        if (action is not (Search or Lookup or "import"))
            throw new ArgumentException($"Unknown {id} action '{action}'.", nameof(action));
        var input = new GameRequest(
            GameCommand.Required(app.Configuration, action is Search ? "query" : "source-id"),
            app.Configuration["game"]
        );
        var rows = await app
            .Services.GetRequiredService<IWorkflowExecutor>()
            .CollectAsync<object>(id + "." + action, input, token);
        GameCommand.Print(rows.Single());
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
