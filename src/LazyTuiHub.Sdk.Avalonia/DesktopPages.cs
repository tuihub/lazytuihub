using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Sdk.Avalonia;

public static class DesktopPages
{
    public static void AddDesktopPage<TPage>(
        this PluginServices registration,
        string id,
        string title,
        string? iconResource = null
    )
        where TPage : Control =>
        registration.AddDesktopPage(
            id,
            title,
            services => ActivatorUtilities.CreateInstance<TPage>(services),
            iconResource
        );

    public static void AddDesktopPage(
        this PluginServices registration,
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
