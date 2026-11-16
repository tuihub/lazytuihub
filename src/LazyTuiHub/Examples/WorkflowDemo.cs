using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Examples;

public static class WorkflowDemo
{
    private const string ProcessName = "process";
    private const string SourceName = "source";
    private const int SampleValue = 2;
    private const int FirstFactor = 3;
    private const int SecondFactor = 5;
    private const int SecondOffset = 3;
    private const int FirstResult = 7;
    private const int SecondResult = 13;
    private const string SecondName = "second";
    private const string FirstName = "first";
    private const string ResultName = "result";
    private const string DataName = "data";
    private const string OffsetName = "offset";
    private const string FactorName = "factor";

    public static async Task<IReadOnlyList<int>> RunAsync(CancellationToken cancellationToken = default)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var state = new DemoState(stopping);
        var registry = new NodeRegistry();
        registry.Register(
            new(
                SourceName,
                "demo",
                [],
                [
                    PortDescriptor.Sync<int>(FactorName),
                    PortDescriptor.Sync<int>(OffsetName),
                    PortDescriptor.Stream<int>(DataName),
                ]
            ),
            state.ProduceAsync
        );
        registry.Register(
            new(
                ProcessName,
                "demo",
                [
                    PortDescriptor.Sync<int>(FactorName),
                    PortDescriptor.Sync<int>(OffsetName),
                    PortDescriptor.Stream<int>(DataName),
                ],
                [PortDescriptor.Stream<int>(ResultName)]
            ),
            state.ProcessAsync
        );
        StreamNodes.Sink<int>(registry, "sink", "demo", (packet, _, _) => state.CollectAsync(packet));
        var runner = new PipelineRunner(registry);
        var plan = runner.CreatePlan(
            new(
                "mixed-inputs",
                [
                    PipelineNode.Create(SourceName, SourceName),
                    PipelineNode.Create(ProcessName, ProcessName),
                    PipelineNode.Create("sink", "sink"),
                ],
                [
                    new(SourceName, FactorName, ProcessName, FactorName),
                    new(SourceName, OffsetName, ProcessName, OffsetName),
                    new(SourceName, DataName, ProcessName, DataName),
                    new(ProcessName, ResultName, "sink", "input"),
                ],
                BufferCapacity: 1
            )
        );
        try
        {
            await runner.RunAsync(plan, cancellationToken: stopping.Token);
        }
        catch (OperationCanceledException)
            when (stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Workflow stopped after receiving the expected results.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.Results.SequenceEqual([SampleValue, FirstResult, SecondResult]))
            throw new InvalidOperationException("Mixed-input execution produced unexpected results.");
        Console.WriteLine(
            "Workflow MVP: partial synchronization leaves streams active; grouped configuration produces 2, 7, 13; cancellation completes."
        );
        return state.Results;
    }

    private sealed class DemoState(CancellationTokenSource stopping)
    {
        private readonly TaskCompletionSource[] received = Enumerable.Range(0, 3).Select(_ => Signal()).ToArray();
        private readonly TaskCompletionSource[] configured = Enumerable.Range(0, 2).Select(_ => Signal()).ToArray();
        public List<int> Results { get; } = [];

        public async Task ProduceAsync(NodeContext context, CancellationToken token)
        {
            await context.WriteAsync(FactorName, FirstFactor, token, messageKey: FirstName);
            await context.WriteAsync(DataName, SampleValue, token);
            await received[0].Task.WaitAsync(token);
            await context.WriteAsync(OffsetName, 1, token, messageKey: FirstName);
            await configured[0].Task.WaitAsync(token);
            await context.WriteAsync(DataName, SampleValue, token);
            await received[1].Task.WaitAsync(token);
            await context.WriteAsync(OffsetName, SecondOffset, token, messageKey: SecondName);
            await context.WriteAsync(FactorName, SecondFactor, token, messageKey: SecondName);
            await configured[1].Task.WaitAsync(token);
            await context.WriteAsync(DataName, SampleValue, token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        public async Task ProcessAsync(NodeContext context, CancellationToken token)
        {
            var options = new Options(1, 0);
            await foreach (var input in context.ReadEventsAsync())
            {
                if (input is SynchronizedInput sync)
                {
                    options = new(sync.Get<int>(FactorName), sync.Get<int>(OffsetName));
                    configured[string.Equals(sync.MessageKey, FirstName, StringComparison.Ordinal) ? 0 : 1]
                        .TrySetResult();
                }
                else if (input is StreamInput data)
                {
                    var snapshot = options;
                    await context.WriteAsync(ResultName, data.Get<int>() * snapshot.Factor + snapshot.Offset, token);
                }
            }
        }

        public async ValueTask CollectAsync(DataPacket<int> packet)
        {
            Results.Add(packet.Value);
            received[Results.Count - 1].TrySetResult();
            if (Results.Count == received.Length)
                await stopping.CancelAsync();
        }

        private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed record Options(int Factor, int Offset);
    }
}
