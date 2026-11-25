namespace LazyTuiHub.Plugins;

public sealed record PluginEntryPoints(
    string? Server = null,
    string? Client = null,
    string? ClientGui = null,
    string? ClientCli = null
)
{
    internal IEnumerable<(PluginEntryPointKind Kind, string Type)> For(FrontendKind frontend)
    {
        if (frontend == FrontendKind.None)
            yield break;
        if (Client is not null)
            yield return (PluginEntryPointKind.Client, Client);
        if (frontend == FrontendKind.Gui && ClientGui is not null)
            yield return (PluginEntryPointKind.ClientGui, ClientGui);
        if (frontend == FrontendKind.Cli && ClientCli is not null)
            yield return (PluginEntryPointKind.ClientCli, ClientCli);
    }
}
