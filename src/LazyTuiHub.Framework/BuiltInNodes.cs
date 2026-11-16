using System.Globalization;
using System.Runtime.CompilerServices;

namespace LazyTuiHub.Framework;

public static class BuiltInNodes
{
    public static void Register(NodeRegistry registry, HttpClient httpClient)
    {
        registry.RegisterSource<TimerTick>("framework.timer", "framework", Ticks);
        registry.RegisterTransform<FetchRequest, HttpDocument>("framework.http-fetch", "framework", Fetch);
        registry.RegisterSink<object>("framework.output", "framework", (value, reference, node, context, token) =>
            context.Output?.Invoke(new DataPacket<object>(value, reference), token) ?? ValueTask.CompletedTask
        );

        async IAsyncEnumerable<DataPacket<HttpDocument>> Fetch(
            DataPacket<FetchRequest> packet,
            PipelineNode node,
            PipelineContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            using var response = await httpClient.GetAsync(
                packet.Value.Uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            yield return new(new(packet.Value.Uri, content, packet.Value.Source), packet.Value.Source);
        }
    }

    private static async IAsyncEnumerable<DataPacket<TimerTick>> Ticks(
        PipelineNode node,
        PipelineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var timer = context.Continuous
            ? new PeriodicTimer(
                TimeSpan.Parse(node.Settings.GetProperty("interval").GetString()!, CultureInfo.InvariantCulture)
            )
            : null;
        yield return new(new(DateTimeOffset.UtcNow));
        if (timer is null)
            yield break;
        while (await timer.WaitForNextTickAsync(cancellationToken))
            yield return new(new(DateTimeOffset.UtcNow));
    }
}
