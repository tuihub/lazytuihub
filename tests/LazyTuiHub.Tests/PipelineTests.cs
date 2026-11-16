using System.Runtime.CompilerServices;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class PipelineTests
{
    private static readonly TimeSpan DeadlockGuard = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task BroadcastSharesPayloadsAndBackpressuresTheSource()
    {
        using var stopping = new CancellationTokenSource(DeadlockGuard);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdProduced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var payloads = Enumerable.Range(0, 20).Select(value => new Payload(value)).ToArray();
        var fast = new List<Payload>();
        var slow = new List<Payload>();
        var produced = 0;
        var registry = new NodeRegistry();
        StreamNodes.Source<Payload>(registry, "source", "first-business", (_, _) => payloads
                .Select(payload =>
                {
                    if (Interlocked.Increment(ref produced) == 3)
                        thirdProduced.TrySetResult();
                    return new DataPacket<Payload>(payload);
                })
                .ToAsyncEnumerable());
        StreamNodes.Sink<Payload>(registry, "slow", "second-business", async (packet, _, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            slow.Add(packet.Value);
        });
        StreamNodes.Sink<Payload>(registry, "fast", "third-business", (packet, _, _) =>
        {
            fast.Add(packet.Value);
            return ValueTask.CompletedTask;
        });
        var definition = new PipelineDefinition(
            "broadcast",
            [
                PipelineNode.Create("source", "source"),
                PipelineNode.Create("slow", "slow"),
                PipelineNode.Create("fast", "fast"),
            ],
            [new("source", "output", "slow", "input"), new("source", "output", "fast", "input")],
            BufferCapacity: 1
        );
        var run = new PipelineRunner(registry).RunAsync(definition, cancellationToken: stopping.Token);
        try
        {
            await entered.Task.WaitAsync(stopping.Token);
            await thirdProduced.Task.WaitAsync(stopping.Token);
            Assert.InRange(Volatile.Read(ref produced), 1, 3);
            Assert.False(run.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }
        await run.WaitAsync(stopping.Token);
        Assert.Equal(payloads.Length, fast.Count);
        Assert.Equal(payloads.Length, slow.Count);
        for (var i = 0; i < payloads.Length; i++)
        {
            Assert.Same(payloads[i], fast[i]);
            Assert.Same(payloads[i], slow[i]);
        }
    }

    [Fact]
    public async Task FailureCancelsAConsumerBlockedOnAnotherBranch()
    {
        using var stopping = new CancellationTokenSource(DeadlockGuard);
        var registry = new NodeRegistry();
        StreamNodes.Source<string>(registry, "source", "test", (_, _) =>
            Enumerable.Repeat(new DataPacket<string>("item"), 100).ToAsyncEnumerable()
        );
        StreamNodes.Sink<string>(registry, "fail", "test", (_, _, _) =>
            ValueTask.FromException(new InvalidOperationException("broken node"))
        );
        StreamNodes.Sink<string>(registry, "blocked", "test", async (_, _, token) =>
            await Task.Delay(Timeout.Infinite, token)
        );
        var definition = new PipelineDefinition(
            "failure",
            [
                PipelineNode.Create("source", "source"),
                PipelineNode.Create("failure", "fail"),
                PipelineNode.Create("blocked", "blocked"),
            ],
            [new("source", "output", "blocked", "input"), new("source", "output", "failure", "input")],
            BufferCapacity: 1
        );
        var failure = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
            new PipelineRunner(registry)
                .RunAsync(definition, cancellationToken: stopping.Token)
                .WaitAsync(stopping.Token)
        );
        Assert.Equal("failure", failure.NodeId);
        Assert.IsType<InvalidOperationException>(failure.InnerException);
    }

    [Fact]
    public async Task FanInCompletesOnlyAfterEveryProducerFinishes()
    {
        using var stopping = new CancellationTokenSource(DeadlockGuard);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var values = new List<string>();
        var registry = new NodeRegistry();
        StreamNodes.Source<string>(registry, "first", "test", (_, _) =>
            new[] { new DataPacket<string>("first") }.ToAsyncEnumerable()
        );
        StreamNodes.Source<string>(registry, "second", "test", (_, token) => Delayed(token));
        StreamNodes.Sink<string>(registry, "sink", "test", (packet, _, _) =>
        {
            values.Add(packet.Value);
            received.TrySetResult();
            return ValueTask.CompletedTask;
        });
        var definition = new PipelineDefinition(
            "merge",
            [PipelineNode.Create("a", "first"), PipelineNode.Create("b", "second"), PipelineNode.Create("out", "sink")],
            [new("a", "output", "out", "input"), new("b", "output", "out", "input")],
            BufferCapacity: 1
        );
        var run = new PipelineRunner(registry).RunAsync(definition, cancellationToken: stopping.Token);
        await received.Task.WaitAsync(stopping.Token);
        Assert.False(run.IsCompleted);
        release.TrySetResult();
        await run.WaitAsync(stopping.Token);
        Assert.Equal(["first", "second"], values);

        async IAsyncEnumerable<DataPacket<string>> Delayed([EnumeratorCancellation] CancellationToken token)
        {
            await release.Task.WaitAsync(token);
            yield return new("second");
        }
    }

    [Fact]
    public async Task InvalidTypesAndCyclesAreRejectedBeforeSourcesRun()
    {
        var started = false;
        var registry = new NodeRegistry();
        StreamNodes.Source<string>(registry, "source", "test", (_, _) =>
        {
            started = true;
            return new[] { new DataPacket<string>("text") }.ToAsyncEnumerable();
        });
        StreamNodes.Transform<string, string>(registry, "pass", "test", (packet, _, _) =>
            new[] { packet }.ToAsyncEnumerable()
        );
        StreamNodes.Sink<Uri>(registry, "uri", "test", (_, _, _) => ValueTask.CompletedTask);
        StreamNodes.Sink<string>(registry, "sink", "test", (_, _, _) => ValueTask.CompletedTask);
        var runner = new PipelineRunner(registry);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunAsync(
                new PipelineDefinition(
                    "bad type",
                    [PipelineNode.Create("source", "source"), PipelineNode.Create("out", "uri")],
                    [new("source", "output", "out", "input")]
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunAsync(
                new PipelineDefinition(
                    "cycle",
                    [
                        PipelineNode.Create("source", "source"),
                        PipelineNode.Create("a", "pass"),
                        PipelineNode.Create("b", "pass"),
                        PipelineNode.Create("out", "sink"),
                    ],
                    [
                        new("source", "output", "a", "input"),
                        new("a", "output", "b", "input"),
                        new("b", "output", "a", "input"),
                        new("b", "output", "out", "input"),
                    ]
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.False(started);
    }

    private sealed record Payload(int Value);

    [Fact]
    public async Task TypedLibraryNodesPreserveBusinessPayloadReferences()
    {
        var payload = new Payload(42);
        var reference = new DataReference("example.payload", "42");
        DataPacket? captured = null;
        var registry = new NodeRegistry();
        BuiltInNodes.RegisterOutput<Payload>(registry, "capture");
        StreamNodes.Source<Payload>(registry, "source", "business", (_, _) =>
            new[] { new DataPacket<Payload>(payload, reference) }.ToAsyncEnumerable()
        );
        var runner = new PipelineRunner(registry);
        var plan = runner.CreatePlan(
            new(
                "typed",
                [PipelineNode.Create("source", "source"), PipelineNode.Create("out", "capture")],
                [new("source", "output", "out", "input")]
            )
        );
        Assert.All(plan.Nodes, node => Assert.Equal("local", node.Instance));
        await runner.RunAsync(
            plan,
            new(
                Output: (packet, _) =>
                {
                    captured = packet;
                    return ValueTask.CompletedTask;
                }
            ),
            TestContext.Current.CancellationToken
        );
        Assert.Same(payload, captured!.Payload);
        Assert.Equal(reference, captured.Reference);
    }
}
