using LazyTuiHub.Client;
using LazyTuiHub.Desktop;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk.Avalonia;

namespace LazyTuiHub;

internal static class RemoteDesktop
{
    public static void Configure(HostApplicationBuilder builder, RemoteWorkspace remote, FrontendKind frontend)
    {
        var config = builder.Configuration;
        builder.Services.AddSingleton(_ => remote);
        var pages = remote.Configure(
            builder.Services,
            config["plugin-directory"] ?? Path.Combine(AppContext.BaseDirectory, "plugins"),
            frontend,
            Console.Error.WriteLine
        );
        foreach (var (owner, page) in pages)
            builder.Services.AddSingleton(
                new RemotePage(page.Title, page.IconResource ?? "ActionIconBook", services =>
                    DesktopPages.Resolve(services, owner, page.Id)
                )
            );
    }
}
