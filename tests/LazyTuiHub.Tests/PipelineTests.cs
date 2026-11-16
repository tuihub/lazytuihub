using System.Runtime.CompilerServices;
using LazyTuiHub.Framework;
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
        registry.RegisterSource<Payload>("source", "first-business", (_, _, _) => payloads
                .Select(payload =>
                {
                    if (Interlocked.Increment(ref produced) == 3)
                        thirdProduced.TrySetResult();
                    return new DataPacket<Payload>(payload);
                })
                .ToAsyncEnumerable());
        registry.RegisterSink<Payload>("slow", "second-business", async (payload, _, _, _, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            slow.Add(payload);
        });
        registry.RegisterSink<Payload>("fast", "third-business", (payload, _, _, _, _) =>
        {
            fast.Add(payload);
            return ValueTask.CompletedTask;
        });
        var definition = new PipelineDefinition(
            "broadcast",
            [
                PipelineNode.Create("source", "source"),
                PipelineNode.Create("slow", "slow"),
                PipelineNode.Create("fast", "fast"),
            ],
            [new("source", "slow"), new("source", "fast")],
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
        registry.RegisterSource<string>("source", "test", (_, _, _) =>
            Enumerable.Repeat(new DataPacket<string>("item"), 100).ToAsyncEnumerable()
        );
        registry.RegisterSink<string>("fail", "test", (_, _, _, _, _) =>
            ValueTask.FromException(new InvalidOperationException("broken node"))
        );
        registry.RegisterSink<string>("blocked", "test", async (_, _, _, _, token) =>
            await Task.Delay(Timeout.Infinite, token)
        );
        var definition = new PipelineDefinition(
            "failure",
            [
                PipelineNode.Create("source", "source"),
                PipelineNode.Create("failure", "fail"),
                PipelineNode.Create("blocked", "blocked"),
            ],
            [new("source", "blocked"), new("source", "failure")],
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
        registry.RegisterSource<string>("first", "test", (_, _, _) =>
            new[] { new DataPacket<string>("first") }.ToAsyncEnumerable()
        );
        registry.RegisterSource<string>("second", "test", (_, _, token) => Delayed(token));
        registry.RegisterSink<string>("sink", "test", (value, _, _, _, _) =>
        {
            values.Add(value);
            received.TrySetResult();
            return ValueTask.CompletedTask;
        });
        var definition = new PipelineDefinition(
            "merge",
            [PipelineNode.Create("a", "first"), PipelineNode.Create("b", "second"), PipelineNode.Create("out", "sink")],
            [new("a", "out"), new("b", "out")],
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
        registry.RegisterSource<string>("source", "test", (_, _, _) =>
        {
            started = true;
            return new[] { new DataPacket<string>("text") }.ToAsyncEnumerable();
        });
        registry.RegisterTransform<string, string>("pass", "test", (packet, _, _, _) =>
            new[] { packet }.ToAsyncEnumerable()
        );
        registry.RegisterSink<Uri>("uri", "test", (_, _, _, _, _) => ValueTask.CompletedTask);
        registry.RegisterSink<string>("sink", "test", (_, _, _, _, _) => ValueTask.CompletedTask);
        var runner = new PipelineRunner(registry);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunAsync(
                new(
                    "bad type",
                    [PipelineNode.Create("source", "source"), PipelineNode.Create("out", "uri")],
                    [new("source", "out")]
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunAsync(
                new(
                    "cycle",
                    [
                        PipelineNode.Create("source", "source"),
                        PipelineNode.Create("a", "pass"),
                        PipelineNode.Create("b", "pass"),
                        PipelineNode.Create("out", "sink"),
                    ],
                    [new("source", "a"), new("a", "b"), new("b", "a"), new("b", "out")]
                ),
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.False(started);
    }

    private sealed record Payload(int Value);

    [Fact]
    public async Task GenericNodesCanConsumeBusinessDataWithoutCopyingItsPayload()
    {
        var payload = new Payload(42);
        var reference = new DataReference("example.payload", "42");
        DataPacket? captured = null;
        var registry = new NodeRegistry();
        using var http = new HttpClient();
        BuiltInNodes.Register(registry, http);
        registry.RegisterSource<Payload>("source", "business", (_, _, _) =>
            new[] { new DataPacket<Payload>(payload, reference) }.ToAsyncEnumerable()
        );
        registry.RegisterTransform<object, object>("pass", "generic-business", (packet, _, _, _) =>
            new[] { packet }.ToAsyncEnumerable()
        );
        var definition = new PipelineDefinition(
            "generic",
            [
                PipelineNode.Create("source", "source"),
                PipelineNode.Create("pass", "pass"),
                PipelineNode.Create("out", "framework.output"),
            ],
            [new("source", "pass"), new("pass", "out")]
        );
        await new PipelineRunner(registry).RunAsync(
            definition,
            new(
                Output: (packet, _) =>
                {
                    captured = packet;
                    return ValueTask.CompletedTask;
                }
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Same(payload, captured!.Payload);
        Assert.Equal(reference, captured.Reference);
    }
}
