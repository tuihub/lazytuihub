using System.Runtime.ExceptionServices;

namespace LazyTuiHub.Framework;

public sealed class PipelineRunner(NodeRegistry registry)
{
    public PipelinePlan CreatePlan(PipelineDefinition definition) => new PipelinePlanner(registry).Create(definition);

    public Task RunAsync(
        PipelineDefinition definition,
        PipelineContext? context = null,
        CancellationToken cancellationToken = default
    ) => RunAsync(CreatePlan(definition), context, cancellationToken);

    public async Task RunAsync(
        PipelinePlan plan,
        PipelineContext? context = null,
        CancellationToken cancellationToken = default
    )
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var run = context ?? new();
        var inboxes = plan.Nodes.ToDictionary(
            node => node.Definition.Id,
            node => new NodeInbox(node, plan, run.Progress),
            StringComparer.Ordinal
        );
        Exception? failure = null;
        await Task.WhenAll(plan.Nodes.Select(node => Task.Run(() => ExecuteAsync(node), CancellationToken.None)));
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        cancellationToken.ThrowIfCancellationRequested();

        async Task ExecuteAsync(PlannedNode node)
        {
            var outgoing = plan
                .Connections.Where(edge => string.Equals(edge.Source, node.Definition.Id, StringComparison.Ordinal))
                .ToArray();
            try
            {
                stopping.Token.ThrowIfCancellationRequested();
                run.Progress?.Invoke(new(node.Definition.Id, "running"));
                var inputs = inboxes[node.Definition.Id].ReadAsync(stopping.Token);
                var nodeContext = new NodeContext(node, run, inputs, PublishAsync);
                await node.Registration.Run(nodeContext, stopping.Token);
                inboxes[node.Definition.Id].Stop();
                foreach (var edge in outgoing)
                    await inboxes[edge.Target].CompleteAsync(edge.TargetPort, stopping.Token);
                run.Progress?.Invoke(new(node.Definition.Id, "completed"));
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                run.Progress?.Invoke(new(node.Definition.Id, "canceled"));
            }
            catch (Exception error)
            {
                Interlocked.CompareExchange(
                    ref failure,
                    new PipelineExecutionException(node.Definition.Id, error),
                    null
                );
                await stopping.CancelAsync();
            }

            async ValueTask PublishAsync(string port, DataPacket packet, string? key, CancellationToken token)
            {
                foreach (
                    var edge in outgoing.Where(edge => string.Equals(edge.SourcePort, port, StringComparison.Ordinal))
                )
                    await inboxes[edge.Target].WriteAsync(edge.TargetPort, packet, key, token);
            }
        }
    }
}
