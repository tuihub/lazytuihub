using Avalonia.Controls;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Desktop;

public static class DesktopPages
{
    public static void AddDesktopPage(
        this PluginRegistration registration,
        string id,
        string title,
        Func<IServiceProvider, Control> create,
        string? iconResource = null
    )
    {
        registration.Pages.Add(new(id, title, iconResource));
        registration.Services.AddKeyedTransient<Control>((registration.Id, id), (services, _) => create(services));
    }

    public static Control Resolve(IServiceProvider services, string plugin, string page) =>
        services.GetRequiredKeyedService<Control>((plugin, page));
}
