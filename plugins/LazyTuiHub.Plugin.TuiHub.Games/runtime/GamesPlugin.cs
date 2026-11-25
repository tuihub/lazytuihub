using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games;

public sealed class GamesPlugin : IApplicationPlugin
{
    internal const string Group = "games";

    public void Configure(PluginRegistration registration)
    {
        registration.Daos.Requirements.Add(new("database", DaoMode.Native, DaoContracts.Relational, Backend: "sqlite"));
        registration.Services.AddSingleton(services => new GameLibrary(
            registration.Daos,
            registration.Storage,
            services.GetRequiredService<ResourceAccess>(),
            services.GetRequiredService<GameIdIndex>()
        ));
        registration.Services.AddSingleton<GameIdIndex>();
        registration.Services.AddSingleton<GameConnections>();
        registration.Services.AddSingleton<GameCuration>();
        registration.Services.AddSingleton<IInstallationScanner, FolderScanner>();
        registration.DataTypes.Add("games.game");
        registration.AddCapabilities<GameOperations>(Group);
        registration.RegisterNodes = (nodes, services) =>
        {
            var library = services.GetRequiredService<GameLibrary>();
            StreamNodes.Source<string>(nodes, "games.query", Group, (context, _) =>
            {
                var text = (context.Run.Input as GameRequest)?.Text
                    ?? context.Node.Settings.GetProperty("text").GetString();
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("The game query needs a title or source ID.");
                return new[] { new DataPacket<string>(text) }.ToAsyncEnumerable();
            });
            StreamNodes.Transform<GameMetadata, GameImport>(
                nodes,
                "games.prepare-import",
                Group,
                (packet, context, _) =>
                    new[]
                    {
                        new DataPacket<GameImport>(new(packet.Value, (context.Run.Input as GameRequest)?.Target)),
                    }.ToAsyncEnumerable()
            );
            nodes.RegisterOwned<GameImport, GameEntry>(
                new(
                    "games.store",
                    Group,
                    [PortDescriptor.Stream<GameImport>(StreamNodes.Input)],
                    [PortDescriptor.Stream<GameEntry>(StreamNodes.Output)],
                    ExecutionTopology.PrimaryWorkspace,
                    new(GameProviderPlugin.GameLibraryId, Group, AccessMode.Write)
                ),
                (packet, _, _) =>
                {
                    var entry = library.Import(packet.Value.Metadata, packet.Value.Target);
                    var account = services.GetRequiredService<ResourceAccess>().Account;
                    return Task.FromResult(
                        new DataPacket<GameEntry>(entry, new DataReference("games.game", entry.Id, account))
                    );
                }
            );
            StreamNodes.RegisterOutput<GameMetadata[]>(nodes, "games.search-output", Group);
            StreamNodes.RegisterOutput<GameMetadata>(nodes, "games.lookup-output", Group);
            StreamNodes.RegisterOutput<GameEntry>(nodes, "games.import-output", Group);
        };
    }
}
