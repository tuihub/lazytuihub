using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using LazyTuiHub.Rss;

namespace LazyTuiHub.Commands;

internal static class RssCommand
{
    public static async Task RunAsync(WebApplication app, string command)
    {
        if (command is "help")
        {
            Console.WriteLine(
                """
                RSS: add --feed-url URI [--title TEXT], feeds, refresh --name feeds/ID,
                     articles [--parent feeds/ID] [--unread true], read --name feeds/ID/articles/ID
                Pipelines: nodes, pipeline [--pipeline-name NAME], save-pipeline --pipeline-file FILE,
                           run or watch [--pipeline-file FILE], relations --feed-id ID
                Demo: demo
                Pass --database PATH to select a workspace. Press Ctrl+C to stop watch.
                """
            );
            return;
        }
        if (command is "demo")
        {
            await RssDemo.RunAsync();
            return;
        }
        ConsoleCancelEventHandler cancel = (_, args) =>
        {
            args.Cancel = true;
            app.Lifetime.StopApplication();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            await ExecuteAsync(app, command, app.Lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            Console.WriteLine("Stopped.");
        }
        catch (RpcException exception)
        {
            await Console.Error.WriteLineAsync(
                $"{exception.StatusCode}: {exception.Status.Detail}".AsMemory(),
                CancellationToken.None
            );
            Environment.ExitCode = 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    private static async Task ExecuteAsync(WebApplication app, string command, CancellationToken cancellationToken)
    {
        var configuration = app.Configuration;
        var api = app.Services.GetRequiredService<IRssApi>();
        switch (command)
        {
            case "add":
                Print(
                    await api.CreateFeedAsync(
                        new()
                        {
                            Feed = new()
                            {
                                Uri = Required(configuration, "feed-url"),
                                DisplayName = configuration["title"] ?? string.Empty,
                            },
                            FeedId = configuration["feed-id"] ?? string.Empty,
                            RequestId = configuration["request-id"] ?? Guid.NewGuid().ToString("D"),
                        },
                        cancellationToken
                    )
                );
                break;
            case "feeds":
                Print(
                    await api.ListFeedsAsync(
                        new() { PageToken = configuration["page-token"] ?? string.Empty },
                        cancellationToken
                    )
                );
                break;
            case "refresh":
                Print(await api.RefreshFeedAsync(new() { Name = Required(configuration, "name") }, cancellationToken));
                break;
            case "articles":
                Print(
                    await api.ListArticlesAsync(
                        new()
                        {
                            Parent = configuration["parent"] ?? "feeds/-",
                            Filter = configuration.GetValue<bool>("unread") ? "read = false" : string.Empty,
                            PageToken = configuration["page-token"] ?? string.Empty,
                        },
                        cancellationToken
                    )
                );
                break;
            case "read":
                Print(
                    await api.MarkArticleReadAsync(new() { Name = Required(configuration, "name") }, cancellationToken)
                );
                break;
            default:
                await ExecutePipelineAsync(
                    app.Services.GetRequiredService<RssWorkspace>(),
                    configuration,
                    command,
                    cancellationToken
                );
                break;
        }
    }

    private static async Task ExecutePipelineAsync(
        RssWorkspace workspace,
        IConfiguration configuration,
        string command,
        CancellationToken cancellationToken
    )
    {
        switch (command)
        {
            case "nodes":
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        workspace.Registry.Descriptors.Select(node => new
                        {
                            node.Type,
                            node.Module,
                            Input = node.InputType?.Name,
                            Output = node.OutputType?.Name,
                        }),
                        PipelineStore.JsonOptions
                    )
                );
                break;
            case "pipeline":
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        workspace.Pipelines.Get(configuration["pipeline-name"] ?? "rss.ingest"),
                        PipelineStore.JsonOptions
                    )
                );
                break;
            case "save-pipeline":
                var definition = await LoadPipelineAsync(configuration, cancellationToken);
                workspace.Runner.Validate(definition);
                workspace.Pipelines.Save(definition);
                Console.WriteLine($"Saved pipeline: {definition.Name}");
                break;
            case "run":
            case "watch":
                var pipeline = configuration["pipeline-file"] is null
                    ? workspace.Pipelines.Get(configuration["pipeline-name"] ?? "rss.ingest")
                    : await LoadPipelineAsync(configuration, cancellationToken);
                await workspace.Runner.RunAsync(
                    pipeline,
                    new(Output: WritePacket, Continuous: command is "watch"),
                    cancellationToken
                );
                break;
            case "relations":
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        workspace.Catalog.ReadRelations(new("rss.feed", Required(configuration, "feed-id"))),
                        PipelineStore.JsonOptions
                    )
                );
                break;
            default:
                throw new ArgumentException(
                    "Choose add, feeds, refresh, articles, read, nodes, pipeline, save-pipeline, run, watch, relations, or demo.",
                    nameof(command)
                );
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException($"Provide --{key}.", nameof(configuration));

    private static async Task<PipelineDefinition> LoadPipelineAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken
    ) =>
        JsonSerializer.Deserialize<PipelineDefinition>(
            await File.ReadAllTextAsync(Required(configuration, "pipeline-file"), cancellationToken),
            PipelineStore.JsonOptions
        ) ?? throw new FormatException("The pipeline file is empty.");

    private static ValueTask WritePacket(DataPacket packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(
            JsonSerializer.Serialize(new { packet.Reference, packet.Payload }, PipelineStore.JsonOptions)
        );
        return ValueTask.CompletedTask;
    }

    private static void Print(IMessage message) => Console.WriteLine(JsonFormatter.Default.Format(message));
}
