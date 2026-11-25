using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LazyTuiHub.Framework;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Desktop;

public sealed partial class ShellWindow : Window
{
    private readonly IHost host;
    private readonly DesktopSession session;
    private readonly DesktopAppearance appearance;
    private readonly Dictionary<NavigationPage, Control> views = [];
    private bool stopped;
    private bool closing;
    private bool replaced;
    public IReadOnlyList<NavigationPage> Navigation { get; }

    public ShellWindow(IHost host, int selectedPage = 0)
    {
        this.host = host;
        session = host.Services.GetRequiredService<DesktopSession>();
        appearance = host.Services.GetRequiredService<DesktopAppearance>();
        appearance.Initialize((DesktopApp)Application.Current!);
        InitializeComponent();
        var plugins = host.Services.GetRequiredService<PluginRuntime>();
        var access = host.Services.GetRequiredService<ResourceAccess>();
        Navigation =
        [
            .. plugins
                .List()
                .Where(plugin => string.Equals(plugin.Activation, "active", StringComparison.Ordinal))
                .SelectMany(plugin =>
                    plugin.Saved.Declaration.Pages.Select(page => new NavigationPage(
                        page.Title,
                        () => DesktopPages.Resolve(host.Services, plugin.Saved.Spec.Id, page.Id),
                        NavigationIcon(page.IconResource)
                    ))
                ),
            .. access.Allows(ResourceUri.SystemControl("operations"), AccessMode.Write) ? ManagementPages(plugins) : [],
        ];
        if (OperatingSystem.IsMacOS())
            InstallViewMenu();
        Pages.ItemsSource = Navigation;
        Pages.SelectedIndex = selectedPage;
        UpdateNavigation(new Size(Width, Height));
        ShowAppearance();
        NavigationSplit.PropertyChanged += (_, e) =>
        {
            if (e.Property == SplitView.IsPaneOpenProperty)
                UpdateNavigationLabels();
        };
        AddHandler(KeyDownEvent, CloseNavigationOnEscape, RoutingStrategies.Tunnel);
        Pages.AddHandler(TappedEvent, InvokeDestination, handledEventsToo: true);
        Closing += WindowClosing;
        SizeChanged += (_, e) => UpdateNavigation(e.NewSize);
    }

    private static Geometry? NavigationIcon(string? resource) =>
        Application.Current!.FindResource(resource ?? "ActionIconGrid") as Geometry
        ?? Application.Current!.FindResource("ActionIconGrid") as Geometry;

    private void InstallViewMenu()
    {
        var view = new NativeMenu();
        var toggle = new NativeMenuItem("Hide Sidebar") { Gesture = new(Key.S, KeyModifiers.Meta | KeyModifiers.Alt) };
        toggle.Click += (_, _) => NavigationSplit.IsPaneOpen = !NavigationSplit.IsPaneOpen;
        view.NeedsUpdate += (_, _) =>
        {
            toggle.IsEnabled = navigationLayout is not (NavigationLayout.MaterialBar or NavigationLayout.MaterialRail);
            toggle.Header = NavigationSplit.IsPaneOpen ? "Hide Sidebar" : "Show Sidebar";
        };
        view.Items.Add(toggle);
        view.Items.Add(new NativeMenuItemSeparator());
        foreach (var page in Navigation)
        {
            var destination = new NativeMenuItem(page.Title);
            destination.Click += (_, _) => Pages.SelectedItem = page;
            view.Items.Add(destination);
        }
        NativeMenu.SetMenu(this, new NativeMenu { Items = { new NativeMenuItem("View") { Menu = view } } });
    }

    private NavigationPage[] ManagementPages(PluginRuntime plugins) =>
        [
            new(
                "Workflows",
                () => new WorkflowPage(plugins.Workflows, host.Services.GetRequiredService<WorkflowPageState>()),
                NavigationIcon("ActionIconWorkflow")
            ),
            new(
                "Plugins",
                () =>
                    new PluginsPage(
                        plugins,
                        host.Services.GetRequiredService<PluginsPageState>(),
                        host.Services.GetRequiredService<ILogger<PluginsPage>>()
                    ),
                NavigationIcon("ActionIconPlugin")
            ),
        ];

    private void MenuClick(object? sender, RoutedEventArgs e) =>
        NavigationSplit.IsPaneOpen = !NavigationSplit.IsPaneOpen;

    private void PageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Pages.SelectedItem is not NavigationPage page)
            return;
        PageContent.Content = Resolve(page);
        if (HasOpenOverlay)
            NavigationSplit.IsPaneOpen = false;
    }

    private Control Resolve(NavigationPage page)
    {
        if (views.TryGetValue(page, out var view))
            return view;
        try
        {
            view = page.Create();
        }
        catch (Exception error)
        {
            LogPageFailure(host.Services.GetRequiredService<ILogger<ShellWindow>>(), page.Title, error);
            view = new TextBox
            {
                Text = error.GetBaseException().Message,
                IsReadOnly = true,
                AcceptsReturn = true,
            };
        }
        views.Add(page, view);
        return view;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not open page {Page}.")]
    private static partial void LogPageFailure(ILogger logger, string page, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Desktop shutdown failed.")]
    private static partial void LogShutdownFailure(ILogger logger, Exception exception);

    private async void WindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (stopped || replaced)
            return;
        e.Cancel = true;
        if (closing)
            return;
        closing = true;
        IsEnabled = false;
        Title = "LazyTuiHub · Stopping…";
        await Task.Yield();
        try
        {
            await session.StopAsync();
            await host.StopAsync(CancellationToken.None);
            stopped = true;
            Close();
        }
        catch (Exception error)
        {
            LogShutdownFailure(host.Services.GetRequiredService<ILogger<ShellWindow>>(), error);
            Title = "Could not stop. Close the window to retry.";
            closing = false;
        }
    }
}
