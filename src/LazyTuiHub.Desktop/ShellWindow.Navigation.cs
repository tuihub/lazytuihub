using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace LazyTuiHub.Desktop;

public sealed partial class ShellWindow
{
    private const double FluentMinimalWidth = 640;
    private const double FluentExpandedWidth = 1008;
    private const double MaterialMediumWidth = 600;
    private const double MaterialMediumHeight = 480;
    private const double MaterialRailWidth = 96;
    private const double SidebarWidth = 208;
    private const double SidebarBreakpoint = 992;
    private const int MinimumMaterialDestinations = 3;
    private const int MaximumBarDestinations = 5;
    private const int MaximumRailDestinations = 7;
    private NavigationLayout? navigationLayout;

    private enum NavigationLayout
    {
        Sidebar,
        Drawer,
        FluentCompact,
        FluentExpanded,
        MaterialBar,
        MaterialRail,
    }

    private NavigationLayout ChooseNavigation(Size size)
    {
        if (appearance.EffectiveDesign == DesignSystem.Fluent)
        {
            if (size.Width <= FluentMinimalWidth)
                return NavigationLayout.Drawer;
            return size.Width < FluentExpandedWidth ? NavigationLayout.FluentCompact : NavigationLayout.FluentExpanded;
        }
        if (appearance.EffectiveDesign == DesignSystem.Material)
        {
            if (size.Width < MaterialMediumWidth || size.Height < MaterialMediumHeight)
                return Navigation.Count is >= MinimumMaterialDestinations and <= MaximumBarDestinations
                    ? NavigationLayout.MaterialBar
                    : NavigationLayout.Drawer;
            if (Navigation.Count is >= MinimumMaterialDestinations and <= MaximumRailDestinations)
                return NavigationLayout.MaterialRail;
        }
        return size.Width <= SidebarBreakpoint ? NavigationLayout.Drawer : NavigationLayout.Sidebar;
    }

    private void UpdateNavigation(Size size)
    {
        var layout = ChooseNavigation(size);
        if (layout == navigationLayout)
            return;
        navigationLayout = layout;
        var bottom = layout == NavigationLayout.MaterialBar;
        var rail = layout == NavigationLayout.MaterialRail;
        var fluent = appearance.EffectiveDesign == DesignSystem.Fluent;
        PlaceNavigation(bottom);
        AppearanceTools.Orientation = bottom ? Orientation.Horizontal : Orientation.Vertical;
        CompactHeader.IsVisible = layout == NavigationLayout.Drawer || bottom;
        MenuButton.IsVisible = !bottom;
        PaneToggle.IsVisible = layout is NavigationLayout.FluentCompact or NavigationLayout.FluentExpanded;
        SidebarBrand.IsVisible = appearance.EffectiveDesign == DesignSystem.Material && !bottom && !rail;
        Sidebar.Margin = fluent ? new Thickness(4, 4, 4, 8) : new Thickness(12, 24, 12, 16);
        if (rail)
            Sidebar.Margin = new Thickness(0, 12, 0, 12);
        NavigationSplit.OpenPaneLength = rail ? MaterialRailWidth : SidebarWidth;
        NavigationSplit.DisplayMode = layout switch
        {
            NavigationLayout.FluentCompact => SplitViewDisplayMode.CompactOverlay,
            NavigationLayout.FluentExpanded => SplitViewDisplayMode.CompactInline,
            NavigationLayout.Drawer or NavigationLayout.MaterialBar => SplitViewDisplayMode.Overlay,
            _ => SplitViewDisplayMode.Inline,
        };
        NavigationSplit.IsPaneOpen =
            layout is NavigationLayout.Sidebar or NavigationLayout.FluentExpanded or NavigationLayout.MaterialRail;
        Pages.Classes.Set("material-destinations", rail || bottom);
        Pages.Classes.Set("bottom-destinations", bottom);
        PageSurface.Classes.Set("fluent-layer", PaneToggle.IsVisible);
        PageSurface.BorderThickness = layout switch
        {
            NavigationLayout.FluentExpanded or NavigationLayout.FluentCompact => new Thickness(1, 1, 0, 0),
            NavigationLayout.MaterialBar or NavigationLayout.MaterialRail => default,
            _ => new Thickness(1, 0, 0, 0),
        };
        UpdateNavigationLabels();
    }

    private void PlaceNavigation(bool bottom)
    {
        if (bottom && BottomNavigation.Content is null)
        {
            Sidebar.Children.Remove(Pages);
            Sidebar.Children.Remove(AppearanceTools);
            BottomNavigation.Content = Pages;
            HeaderTools.Content = AppearanceTools;
        }
        else if (!bottom && BottomNavigation.Content is not null)
        {
            BottomNavigation.Content = null;
            HeaderTools.Content = null;
            Sidebar.Children.Add(Pages);
            Sidebar.Children.Add(AppearanceTools);
        }
        BottomNavigation.IsVisible = bottom;
    }

    private bool HasOpenOverlay =>
        NavigationSplit.IsPaneOpen
        && NavigationSplit.DisplayMode is SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay;

    private void CloseNavigationOnEscape(object? sender, KeyEventArgs e)
    {
        var shortcut = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.K && e.KeyModifiers == shortcut)
        {
            OpenOperations(this, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && HasOpenOverlay)
        {
            NavigationSplit.IsPaneOpen = false;
            (PaneToggle.IsVisible ? PaneToggle : MenuButton).Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
    }

    private void InvokeDestination(object? sender, TappedEventArgs e)
    {
        if (
            HasOpenOverlay
            && e.Source is Control source
            && (source as ListBoxItem ?? source.FindAncestorOfType<ListBoxItem>()) is { IsSelected: true }
        )
            NavigationSplit.IsPaneOpen = false;
    }

    private void UpdateNavigationLabels()
    {
        var iconsOnly =
            navigationLayout is NavigationLayout.FluentCompact or NavigationLayout.FluentExpanded
            && !NavigationSplit.IsPaneOpen;
        Sidebar.HorizontalAlignment = iconsOnly ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        Sidebar.Width = iconsOnly
            ? NavigationSplit.CompactPaneLength - Sidebar.Margin.Left - Sidebar.Margin.Right
            : double.NaN;
        Pages.Classes.Set("icons-only", iconsOnly);
        AppearanceLabel.IsVisible =
            !iconsOnly && navigationLayout is not (NavigationLayout.MaterialRail or NavigationLayout.MaterialBar);
        AppearanceIcon.Classes.Set("inline", AppearanceLabel.IsVisible);
        AppearanceButton.Classes.Set("quiet", AppearanceLabel.IsVisible);
        AppearanceButton.Classes.Set("icon", !AppearanceLabel.IsVisible);
        ToolTip.SetTip(AppearanceButton, AppearanceLabel.IsVisible ? null : "Appearance");
    }
}
