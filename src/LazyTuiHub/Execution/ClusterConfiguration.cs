using LazyTuiHub.Framework;

namespace LazyTuiHub.Execution;

internal static class ClusterConfiguration
{
    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHttpClient(nameof(WorkerPeer))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { CheckCertificateRevocationList = true, AllowAutoRedirect = false }
            );
        services.AddSingleton(provider =>
        {
            var cluster = configuration.GetSection("Cluster");
            if (!cluster.Exists())
                return ExecutionTopology.Standalone;
            var clients = provider.GetRequiredService<IHttpClientFactory>();
            var peers = cluster
                .GetSection("Peers")
                .GetChildren()
                .Select(peer =>
                {
                    var address = new Uri(Required(peer, "Url"), UriKind.Absolute);
                    if (
                        !(
                            address.Scheme is "https"
                            || address.Scheme is "http" && cluster.GetValue<bool>("AllowInsecureHttp")
                        )
                    )
                        throw new InvalidOperationException(
                            "Worker connections require HTTPS or an explicitly configured trusted HTTP network."
                        );
                    return new WorkerPeer(address, Required(peer, "Token"), clients);
                })
                .ToArray();
            return new ExecutionTopology(Required(cluster, "Id"), Set(cluster, "Resources"), peers);
        });
    }

    public static HashSet<string> Set(IConfiguration config, string key) =>
        (config.GetSection(key).Get<string[]>() ?? []).ToHashSet(StringComparer.Ordinal);

    public static string Required(IConfiguration config, string key) =>
        config[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing execution instance setting '{key}'.");
}
