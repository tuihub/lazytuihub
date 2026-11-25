using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LazyTuiHub.Desktop;

namespace LazyTuiHub.Games.Desktop;

public sealed partial class GamePage : UserControl
{
    private const double SplitWidth = 840;
    private const int InspectorWidth = 456;
    private const int CompactToolbarWidth = 760;
    private TopLevel? shortcutHost;
    private Action? cancelReveal;
    private (string Game, double Width, Vector Offset)? origin;
    private readonly GamePageModel model;
    private GameScreen? previous;
    private const int CompactHeadingWidth = 600;
    private const int ToolbarToolsWidth = 360;
    private const int ToolbarSpace = 8;
    private const int CompactGroupSpace = 4;
    private const int CompactControlSpace = 4;
    private const int ToolbarColumns = 2;
    private bool restoring;
    private string? inspected;
    internal GameCovers Covers { get; }

    public GamePage(GamePageModel model, GameCovers covers)
    {
        this.model = model;
        Covers = covers;
        InitializeComponent();
        DataContext = model;
        Loaded += async (_, _) =>
        {
            restoring = true;
            States.SelectedItem = model.Filter;
            Category.SelectedItem = model.Category;
            Source.SelectedItem = model.SourceFilter;
            restoring = false;
            model.PropertyChanged += Changed;
            await model.LoadAsync();
            UpdateLayoutMode();
        };
        Unloaded += (_, _) =>
        {
            model.PropertyChanged -= Changed;
            cancelReveal?.Invoke();
        };
        SizeChanged += (_, _) => UpdateLayoutMode();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        shortcutHost = TopLevel.GetTopLevel(this);
        shortcutHost?.AddHandler(KeyDownEvent, FindInShelf, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        shortcutHost?.RemoveHandler(KeyDownEvent, FindInShelf);
        shortcutHost = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void FindInShelf(object? sender, KeyEventArgs e)
    {
        if (
            sender is TopLevel window
            && model.InLibrary
            && Search.IsEffectivelyVisible
            && e.Key == Key.F
            && e.KeyModifiers == window.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers
        )
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
    }

    private void Changed(object? sender, PropertyChangedEventArgs e) => UpdateLayoutMode();

    private void UpdateLayoutMode()
    {
        var split = Bounds.Width >= SplitWidth && model.InDetail;
        ArrangeShelf(split);
        UpdateNavigation(split);
    }

    private void ArrangeShelf(bool split)
    {
        var browsing = model.InLibrary || split;
        Workspace.IsVisible = model.InLibrary || model.InDetail;
        PageHeading.IsVisible = browsing;
        GameCount.Text =
            Bounds.Width < CompactHeadingWidth
                ? model.Shelf.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)
                : model.Count;
        Toolbar.IsVisible = browsing && !model.Empty;
        Library.IsVisible = browsing;
        ContentArea.ColumnDefinitions[1].Width = split ? new GridLength(InspectorWidth) : GridLength.Auto;
        Grid.SetColumn(InspectHost, split ? 1 : 0);
        Inspector.SetSplit(split);
        var compact = Bounds.Width < CompactToolbarWidth;
        var gutter = (double)this.FindResource(compact ? "LibraryCompactGutter" : "LibraryGutter")!;
        var inset = ((Thickness)this.FindResource("TileFrameWidth")!).Left + PosterShelf.TilePadding;
        Library.Margin = new Thickness(
            gutter - inset,
            0,
            split ? gutter / ToolbarColumns : gutter - inset,
            gutter / ToolbarColumns
        );
        PageHeading.Margin = new Thickness(gutter, compact ? CompactGroupSpace : gutter, gutter, 0);
        SourcesPanel.Padding = new Thickness(gutter, 0);
        Workshop.Padding = new Thickness(gutter, 0);
        ArrangeToolbar(compact, gutter);
    }

    private void ArrangeToolbar(bool compact, double gutter)
    {
        Toolbar.Margin = new Thickness(
            gutter,
            compact ? CompactControlSpace : ToolbarSpace,
            gutter,
            compact ? CompactGroupSpace : ToolbarSpace
        );
        States.Classes.Set("compact", compact);
        Toolbar.RowSpacing = compact ? CompactControlSpace : 0;
        Grid.SetColumn(Tools, compact ? 0 : 1);
        Grid.SetColumnSpan(Tools, compact ? ToolbarColumns : 1);
        Grid.SetRow(States, compact ? 1 : 0);
        Grid.SetColumnSpan(States, compact ? ToolbarColumns : 1);
        Tools.Width = compact ? double.NaN : ToolbarToolsWidth;
    }

    private void UpdateNavigation(bool split)
    {
        if (model.InDetail && !string.Equals(inspected, model.Selected?.Entry.Id, StringComparison.Ordinal))
        {
            inspected = model.Selected?.Entry.Id;
            Inspector.ScrollToTop();
        }
        if (previous == model.Screen)
            return;
        var last = previous;
        if (last == GameScreen.Connect)
            Workshop.ClearSecret();
        previous = model.Screen;
        cancelReveal?.Invoke();
        cancelReveal = null;
        var shelfStayedVisible = last == GameScreen.Detail && model.InLibrary && Bounds.Width >= SplitWidth;
        if (last is not null && IsLoaded && !shelfStayedVisible)
            cancelReveal = DesktopMotion.Reveal(
                model.Screen switch
                {
                    GameScreen.Detail => InspectHost,
                    GameScreen.Library => Library,
                    GameScreen.Sources => SourcesPanel,
                    _ => Workshop,
                }
            );
        Dispatcher.UIThread.Post(
            () =>
            {
                if (model.InLibrary && last != null)
                    RestoreShelfFocus();
                else if (model.InSources)
                    SourcesPanel.RestoreFocus();
                else if (
                    model.InDetail && (!split || last is GameScreen.Identify or GameScreen.Edit or GameScreen.Reassign)
                )
                    InspectHost
                        .GetVisualDescendants()
                        .OfType<Button>()
                        .FirstOrDefault(b => b.IsEffectivelyVisible)
                        ?.Focus();
                else if (model.NeedsSheet)
                    Workshop.FocusInput();
            },
            DispatcherPriority.Loaded
        );
    }

    private ScrollViewer CurrentScroll => model.CompactList ? ListScroll : ShelfScroll;

    private ItemsRepeater CurrentShelf => model.CompactList ? Rows : Shelf;

    private void RestoreShelfFocus()
    {
        var index = Array.FindIndex(model.Shelf.ToArray(), card =>
            card.Game.Id.Equals(model.Selected?.Entry.Id, StringComparison.Ordinal)
        );
        if (index >= 0)
        {
            FocusCard(index);
            if (
                origin is { } saved
                && string.Equals(model.Selected?.Entry.Id, saved.Game, StringComparison.Ordinal)
                && Math.Abs(saved.Width - Bounds.Width) < 1
            )
                CurrentScroll.Offset = saved.Offset;
        }
        else if (Search.IsEffectivelyVisible)
            Search.Focus();
        else if (AddButton.IsEffectivelyVisible)
            AddButton.Focus();
    }

    private void FocusCard(int index)
    {
        if (CurrentShelf.GetOrCreateElement(index) is Button b)
        {
            b.BringIntoView();
            b.Focus(NavigationMethod.Directional);
        }
    }

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !model.InLibrary && model.Idle)
        {
            model.Back();
            e.Handled = true;
            return;
        }
        if (
            e.Source is not Control control
            || (control as Button ?? control.FindAncestorOfType<Button>()) is not { DataContext: GameCard } button
        )
            return;
        var shelf = CurrentShelf;
        var index = shelf.GetElementIndex(button);
        if (index < 0)
            return;
        var step = model.CompactList ? 1 : PosterGrid.Columns;
        var dest = e.Key switch
        {
            Key.Left => index - 1,
            Key.Right => index + 1,
            Key.Up => index - step,
            Key.Down => index + step,
            _ => -1,
        };
        if (dest >= 0 && dest < model.Shelf.Count)
        {
            FocusCard(dest);
            e.Handled = true;
        }
    }

    private async void OpenGame(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: GameCard card })
        {
            if (model.InLibrary)
                origin = (card.Game.Id, Bounds.Width, CurrentScroll.Offset);
            await model.SelectAsync(card.Game.Id);
        }
    }

    private void OpenSources(object? sender, RoutedEventArgs args) => model.Go(GameScreen.Sources);

    private void OpenAdd(object? sender, RoutedEventArgs args) => model.StartAdd();

    private void EmptyScan(object? sender, RoutedEventArgs args)
    {
        if (model.Scanners.Count > 0)
            model.StartScan(model.Scanners.FirstOrDefault(s => s.Id is "folder") ?? model.Scanners[0]);
    }

    private void ChangeView(object? sender, RoutedEventArgs args) => model.SetList(!model.CompactList);

    private void ClearSearch(object? sender, RoutedEventArgs args)
    {
        Search.Clear();
        Search.Focus();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs args)
    {
        SearchHint.IsVisible = string.IsNullOrEmpty(Search.Text);
        ClearQuery.IsVisible = !SearchHint.IsVisible;
        if (DataContext is not null)
        {
            model.Search = Search.Text ?? string.Empty;
            model.ApplyFilters();
        }
    }

    private void FilterChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is not null && !restoring && sender is ListBox { SelectedItem: string filter })
            model.SetFilter(filter);
    }

    private void ExtraFilterChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (DataContext is null || restoring)
            return;
        if (sender is not ComboBox { SelectedItem: string value } filter)
            return;
        if (ReferenceEquals(filter, Category))
            model.Category = value;
        else
            model.SourceFilter = value;
        model.ApplyFilters();
    }

    private void ClearFilters(object? sender, RoutedEventArgs args)
    {
        Search.Text = string.Empty;
        Category.SelectedItem = null;
        Source.SelectedItem = null;
        model.Category = null;
        model.SourceFilter = null;
        States.SelectedIndex = 0;
        model.SetFilter("All");
    }
}
