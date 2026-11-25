namespace LazyTuiHub.Desktop;

public interface IWorkspaceView
{
    Task RefreshAsync(IReadOnlySet<string> changedGroups);
}
