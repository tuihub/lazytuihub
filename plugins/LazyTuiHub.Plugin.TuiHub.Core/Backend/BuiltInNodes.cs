using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;

namespace LazyTuiHub.Plugin.TuiHub.Core;

internal static class BuiltInNodes
{
    private const string Module = "builtin";

    public static void Register(
        NodeRegistration registry,
        HttpClient httpClient,
        IReadOnlySet<string>? allowedOrigins = null
    )
    {
        StreamNodes.Source<TimerTick>(registry, "builtin.timer", Module, Ticks);
        var fetch = new FetchOperation(httpClient, allowedOrigins);
        registry.RegisterRepeatable<FetchRequest, HttpDocument>(
            new(
                "builtin.http-fetch",
                Module,
                [PortDescriptor.Stream<FetchRequest>(StreamNodes.Input)],
                [PortDescriptor.Stream<HttpDocument>(StreamNodes.Output)]
            ),
            (request, _, token) => fetch.ExecuteAsync(request, token)
        );
        StreamNodes.Source<string>(registry, "builtin.text", Module, (context, _) =>
            new[] { new DataPacket<string>(context.Node.Settings.GetProperty("text").GetString()!) }.ToAsyncEnumerable()
        );
        registry.RegisterRepeatable<string, string>(
            new(
                "builtin.text-digest",
                Module,
                [PortDescriptor.Stream<string>(StreamNodes.Input)],
                [PortDescriptor.Stream<string>(StreamNodes.Output)]
            ),
            (value, _, _) => Task.FromResult(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value))))
        );
        StreamNodes.RegisterOutput<string>(registry, "builtin.output-text", Module);
    }

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
