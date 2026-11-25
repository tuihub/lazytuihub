using LazyTuiHub.Client;

namespace LazyTuiHub.Desktop;

public interface IRemotePageProvider
{
    RemotePage RemotePage(RemoteWorkspace workspace);
}
