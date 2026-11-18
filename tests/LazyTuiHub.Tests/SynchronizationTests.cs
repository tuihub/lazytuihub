using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class SynchronizationTests
{
    [Fact]
    public async Task SynchronizedConfigurationChangesFutureStreamProcessingAndCancellationStopsTheRun() =>
        Assert.Equal([2, 7, 13], await WorkflowDemo.RunAsync(TestContext.Current.CancellationToken));

    [Fact]
    public async Task KeysAlignIndependentlyAndRepeatedKeysPairInPortOrder()
    {
        var groups = new List<(string Key, int Left, int Right)>();
        var registry = new NodeRegistry();
        registry.Register(
            new("source", "test", [], [PortDescriptor.Sync<int>("a"), PortDescriptor.Sync<int>("b")]),
            async (context, token) =>
            {
                await context.WriteAsync("a", 1, token, messageKey: "x");
                await context.WriteAsync("b", 20, token, messageKey: "y");
                await context.WriteAsync("a", 2, token, messageKey: "x");
                await context.WriteAsync("a", 10, token, messageKey: "y");
                await context.WriteAsync("b", 3, token, messageKey: "x");
                await context.WriteAsync("b", 4, token, messageKey: "x");
            }
        );
        registry.Register(
            new("sink", "test", [PortDescriptor.Sync<int>("left"), PortDescriptor.Sync<int>("right")], []),
            async (context, _) =>
            {
                await foreach (var input in context.ReadEventsAsync())
                {
                    var sync = Assert.IsType<SynchronizedInput>(input);
                    groups.Add((sync.MessageKey, sync.Get<int>("left"), sync.Get<int>("right")));
                }
            }
        );
        var runner = new PipelineRunner(registry);
        await runner.RunAsync(Definition(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal([("y", 10, 20), ("x", 1, 3), ("x", 2, 4)], groups);
    }

    [Fact]
    public async Task ClosedMissingPortExplainsFailureWhileOtherInputsRemainOpen()
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stopping.CancelAfter(TimeSpan.FromSeconds(30));
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new NodeRegistry();
        registry.Register(new("source", "test", [], [PortDescriptor.Sync<int>("a")]), async (context, token) =>
        {
            await context.WriteAsync("a", 1, token, messageKey: "missing");
            await seen.Task.WaitAsync(token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        registry.Register(new("empty", "test", [], [PortDescriptor.Sync<int>("b")]), (_, _) => Task.CompletedTask);
        registry.Register(
            new("sink", "test", [PortDescriptor.Sync<int>("left"), PortDescriptor.Sync<int>("right")], []),
            async (context, _) =>
            {
                await foreach (var input in context.ReadEventsAsync())
                    Assert.Fail($"Unexpected partial input: {input}");
            }
        );
        var definition = new PipelineDefinition(
            "missing",
            [
                PipelineNode.Create("source", "source"),
                PipelineNode.Create("empty", "empty"),
                PipelineNode.Create("sink", "sink"),
            ],
            [new("source", "a", "sink", "left"), new("empty", "b", "sink", "right")]
        );
        var error = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
            new PipelineRunner(registry).RunAsync(
                definition,
                new(Progress: progress =>
                {
                    if (progress.MessageKey == "missing")
                        seen.TrySetResult();
                }),
                stopping.Token
            )
        );
        Assert.Equal("sink", error.NodeId);
        Assert.Contains("missing", error.Message);
        Assert.Contains("right", error.Message);
    }

    [Fact]
    public async Task CancellationInterruptsAnIncompleteSynchronization()
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var registry = new NodeRegistry();
        registry.Register(
            new("source", "test", [], [PortDescriptor.Sync<int>("a"), PortDescriptor.Sync<int>("b")]),
            async (context, token) =>
            {
                await context.WriteAsync("a", 1, token, messageKey: "pending");
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        );
        registry.Register(
            new("sink", "test", [PortDescriptor.Sync<int>("left"), PortDescriptor.Sync<int>("right")], []),
            async (context, _) =>
            {
                await foreach (var input in context.ReadEventsAsync())
                    Assert.Fail($"Unexpected partial input: {input}");
            }
        );
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PipelineRunner(registry).RunAsync(
                Definition(),
                new(Progress: progress =>
                {
                    if (progress.State == "waiting-synchronization")
                        stopping.Cancel();
                }),
                stopping.Token
            )
        );
    }

    [Fact]
    public void PlanningRequiresMatchingPortTypesModesAndNames()
    {
        var registry = new NodeRegistry();
        registry.Register(new("source", "test", [], [PortDescriptor.Stream<string>("text")]), (_, _) =>
            Task.CompletedTask
        );
        registry.Register(new("wide", "test", [PortDescriptor.Stream<object>("input")], []), (_, _) =>
            Task.CompletedTask
        );
        registry.Register(new("sync", "test", [PortDescriptor.Sync<string>("input")], []), (_, _) =>
            Task.CompletedTask
        );
        registry.Register(new("exact", "test", [PortDescriptor.Stream<string>("input")], []), (_, _) =>
            Task.CompletedTask
        );
        var planner = new PipelinePlanner(registry, ExecutionTopology.Standalone.LocalInventory);
        foreach (var target in new[] { "wide", "sync", "exact" })
        {
            var error = Assert.Throws<ArgumentException>(() =>
                planner.Create(
                    new(
                        "invalid",
                        [PipelineNode.Create("source", "source"), PipelineNode.Create("sink", target)],
                        [new("source", target == "exact" ? "missing" : "text", "sink", "input")]
                    )
                )
            );
            Assert.Contains("source.", error.Message);
            Assert.Contains("sink.input", error.Message);
        }
    }

    [Fact]
    public async Task ReturningConsumerUnblocksUpstreamWithAnExplainedFailure()
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stopping.CancelAfter(TimeSpan.FromSeconds(30));
        var registry = new NodeRegistry();
        registry.Register(new("source", "test", [], [PortDescriptor.Stream<int>("output")]), async (context, token) =>
        {
            for (var value = 0; value < 10; value++)
                await context.WriteAsync("output", value, token);
        });
        registry.Register(new("sink", "test", [PortDescriptor.Stream<int>("input")], []), (_, _) => Task.CompletedTask);
        var error = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
            new PipelineRunner(registry).RunAsync(
                new PipelineDefinition(
                    "early-return",
                    [PipelineNode.Create("source", "source"), PipelineNode.Create("sink", "sink")],
                    [new("source", "output", "sink", "input")],
                    BufferCapacity: 1
                ),
                cancellationToken: stopping.Token
            )
        );
        Assert.Equal("source", error.NodeId);
        Assert.IsType<System.Threading.Channels.ChannelClosedException>(error.InnerException);
    }

    private static PipelineDefinition Definition() =>
        new(
            "sync",
            [PipelineNode.Create("source", "source"), PipelineNode.Create("sink", "sink")],
            [new("source", "a", "sink", "left"), new("source", "b", "sink", "right")],
            BufferCapacity: 1
        );
}
