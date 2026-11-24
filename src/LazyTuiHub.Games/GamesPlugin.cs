using LazyTuiHub.Dao;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Games;

public sealed class GamesPlugin : IApplicationPlugin
{
    internal const string Id = "games";

    public void Configure(PluginRegistration registration)
    {
        registration.Daos.Requirements.Add(new("database", DaoMode.Native, DaoContracts.Relational, Backend: "sqlite"));
        registration.Services.AddSingleton(services => new GameLibrary(
            registration.Daos,
            registration.Storage,
            services.GetRequiredService<ResourceAccess>()
        ));
        registration.DataTypes.Add("games.game");
        registration.Commands.Add(new(Id, GameCommand.RunAsync));
        registration.RegisterNodes = (nodes, services) =>
        {
            var library = services.GetRequiredService<GameLibrary>();
            StreamNodes.Source<string>(nodes, "games.query", Id, (context, _) =>
            {
                var text = (context.Run.Input as GameRequest)?.Text
                    ?? context.Node.Settings.GetProperty("text").GetString();
                if (string.IsNullOrWhiteSpace(text))
                    throw new InvalidOperationException("The game query needs a title or source ID.");
                return new[] { new DataPacket<string>(text) }.ToAsyncEnumerable();
            });
            StreamNodes.Transform<GameMetadata, GameImport>(nodes, "games.prepare-import", Id, (packet, context, _) =>
                new[]
                {
                    new DataPacket<GameImport>(new(packet.Value, (context.Run.Input as GameRequest)?.Target)),
                }.ToAsyncEnumerable()
            );
            nodes.RegisterOwned<GameImport, GameEntry>(
                new(
                    "games.store",
                    Id,
                    [PortDescriptor.Stream<GameImport>(StreamNodes.Input)],
                    [PortDescriptor.Stream<GameEntry>(StreamNodes.Output)],
                    ExecutionTopology.PrimaryWorkspace,
                    new(Id, Id, AccessMode.Write)
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
            BuiltInNodes.RegisterOutput<GameMetadata[]>(nodes, "games.search-output");
            BuiltInNodes.RegisterOutput<GameMetadata>(nodes, "games.lookup-output");
            BuiltInNodes.RegisterOutput<GameEntry>(nodes, "games.import-output");
        };
    }
}
