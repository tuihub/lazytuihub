using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyTuiHub.Games.Desktop;

public sealed partial class GameSourcesPanel : UserControl
{
    private Button? lastAction;
    private GamePageModel Model => (GamePageModel)DataContext!;

    public GameSourcesPanel()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            const double columns = 760;
            var narrow = Bounds.Width < columns;
            Sections.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(Directories, narrow ? 0 : 1);
            Grid.SetRow(Directories, narrow ? 1 : 0);
        };
    }

    internal void RestoreFocus()
    {
        if (lastAction is { IsEffectivelyVisible: true, IsLoaded: true })
            lastAction.Focus();
        else
            ReturnToShelf.Focus();
    }

    private void Back(object? s, RoutedEventArgs e) => Model.Back();

    private void Connect(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: GamePlatformGroup { Provider: { } p } } button)
        {
            lastAction = button;
            Model.StartConnect(p);
        }
    }

    private async void Sync(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: GameOriginRow { Origin: var item } })
            await Model.SyncAsync(item.Id);
    }

    private async void Disconnect(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: GameOriginRow { Origin: var item } })
            await Model.DisconnectAsync(item.Id);
    }

    private void ChooseScanner(object? s, RoutedEventArgs e)
    {
        if (s is Button { DataContext: IInstallationScanner scan })
            Model.StartScan(scan);
    }

    private void Rescan(object? s, RoutedEventArgs e)
    {
        if (
            s is Button { DataContext: GameOriginRow { Origin: { CurrentDevicePath: { } path } item } }
            && Model.Scanners.FirstOrDefault(scan => scan.Id.Equals(item.Provider, StringComparison.Ordinal))
                is { } scanner
        )
            Model.StartScan(scanner, path);
    }
}
