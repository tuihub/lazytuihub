namespace LazyTuiHub.Sdk.Avalonia;

public interface IWorkspaceView
{
    Task RefreshAsync(IReadOnlySet<string> changedGroups);
}
