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
    public static PluginDeclaration Empty { get; } = new([], [], [], [], false, false);
}
