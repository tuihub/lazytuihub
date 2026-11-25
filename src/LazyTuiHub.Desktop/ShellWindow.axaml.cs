using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;
using LazyTuiHub.Plugins;
using LazyTuiHub.Sdk.Avalonia;
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
    private readonly ClientSession? identity;
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
        identity = host.Services.GetService<ClientSession>();
        var plugins = host.Services.GetService<PluginRuntime>();
        if (plugins is null)
            Navigation =
            [
                .. host
                    .Services.GetServices<RemotePage>()
                    .Select(p => new NavigationPage(p.Title, () => p.Create(host.Services), NavigationIcon(p.Icon))),
                new("Operations", Operations, NavigationIcon("ActionIconSearch")),
            ];
        else if (identity is { Token: null })
            Navigation = [new("Sign in", Operations, NavigationIcon("ActionIconPlugin"))];
        else
            Navigation = LocalPages(plugins);
        if (identity is not null)
        {
            identity.Changed += SessionChanged;
            Closed += (_, _) => identity.Changed -= SessionChanged;
        }
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

    private IReadOnlyList<NavigationPage> LocalPages(PluginRuntime plugins)
    {
        var access = host.Services.GetRequiredService<ResourceAccess>();
        return
        [
            .. plugins.ActivePages.Select(item => new NavigationPage(
                item.Page.Title,
                () => DesktopPages.Resolve(host.Services, item.Owner, item.Page.Id),
                NavigationIcon(item.Page.IconResource)
            )),
            .. access.Allows(ResourceUri.SystemControl("operations"), AccessMode.Write) ? ManagementPages() : [],
        ];
    }

    private void SessionChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (replaced || closing)
                return;
            if (identity?.Token is null)
                Close();
            else
                ReplaceWindow(0);
        });

    private OperationsPage Operations() => new(host.Services.GetRequiredService<CapabilityCatalog>(), session);

    private async void OpenOperations(object? sender, RoutedEventArgs args)
    {
        var page = Operations();
        var dialog = new Window
        {
            Title = "Operations",
            Content = page,
            Width = Math.Min(Width, 580),
            Height = Math.Min(Height, 680),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Background,
        };
        await dialog.ShowDialog(this);
        if (closing || replaced || page.ExecutedGroups.Count == 0)
            return;
        foreach (var (destination, control) in views.ToArray())
        {
            if (control is not IWorkspaceView view)
                continue;
            try
            {
                await view.RefreshAsync(page.ExecutedGroups);
            }
            catch (Exception error)
            {
                views.Remove(destination);
                if (Equals(Pages.SelectedItem, destination))
                    PageContent.Content = PageFailure(destination, error);
                else
                    LogPageFailure(host.Services.GetRequiredService<ILogger<ShellWindow>>(), destination.Title, error);
            }
        }
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

    private NavigationPage[] ManagementPages() =>
        [
            new(
                "Workflows",
                () =>
                    new WorkflowPage(
                        host.Services.GetRequiredService<WorkflowOperations>(),
                        host.Services.GetRequiredService<WorkflowPageState>()
                    ),
                NavigationIcon("ActionIconWorkflow")
            ),
            new(
                "Plugins",
                () =>
                    new PluginsPage(
                        host.Services.GetRequiredService<PluginOperations>(),
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
            return PageFailure(page, error);
        }
        views.Add(page, view);
        return view;
    }

    private TextBox PageFailure(NavigationPage page, Exception error)
    {
        LogPageFailure(host.Services.GetRequiredService<ILogger<ShellWindow>>(), page.Title, error);
        return new TextBox
        {
            Text = error.GetBaseException().Message,
            IsReadOnly = true,
            AcceptsReturn = true,
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not load page {Page}.")]
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
