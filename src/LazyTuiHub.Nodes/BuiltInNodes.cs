using System.Globalization;
using System.Runtime.CompilerServices;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Nodes;

public static class BuiltInNodes
{
    public static void Register(NodeRegistry registry, HttpClient httpClient)
    {
        StreamNodes.Source<TimerTick>(registry, "builtin.timer", "builtin", Ticks);
        StreamNodes.Transform<FetchRequest, HttpDocument>(registry, "builtin.http-fetch", "builtin", Fetch);

        async IAsyncEnumerable<DataPacket<HttpDocument>> Fetch(
            DataPacket<FetchRequest> packet,
            NodeContext context,
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

    public static void RegisterOutput<T>(NodeRegistry registry, string type)
        where T : notnull =>
        StreamNodes.Sink<T>(registry, type, "builtin", (packet, context, token) =>
            context.Run.Output?.Invoke(packet, token) ?? ValueTask.CompletedTask
        );

    private static async IAsyncEnumerable<DataPacket<TimerTick>> Ticks(
        NodeContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var timer = context.Run.Continuous
            ? new PeriodicTimer(
                TimeSpan.Parse(context.Node.Settings.GetProperty("interval").GetString()!, CultureInfo.InvariantCulture)
            )
            : null;
        yield return new(new(DateTimeOffset.UtcNow));
        if (timer is null)
            yield break;
        while (await timer.WaitForNextTickAsync(cancellationToken))
            yield return new(new(DateTimeOffset.UtcNow));
    }
}
