using LazyTuiHub.Dao;

namespace LazyTuiHub.Plugins;

public sealed record PluginDeclaration(
    IReadOnlyList<DeclaredNode> Nodes,
    IReadOnlyList<string> DataTypes,
    IReadOnlyList<string> Commands,
    IReadOnlyList<DeclaredTask> Tasks,
    bool HasCleanup,
    bool HasUninstall
)
{
    public IReadOnlyList<DaoCapability> DaoOffers { get; init; } = [];
    public IReadOnlyList<DaoRequirement> DaoRequirements { get; init; } = [];
    public IReadOnlyList<DeclaredPage> Pages { get; init; } = [];

    public static PluginDeclaration Empty { get; } = new([], [], [], [], false, false);
}
