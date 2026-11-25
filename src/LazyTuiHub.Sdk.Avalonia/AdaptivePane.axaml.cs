using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LazyTuiHub.Sdk.Avalonia;

public sealed partial class AdaptivePane : UserControl
{
    private const double CompactWidth = 768;
    private const double MasterWidth = 360;
    private const int DetailColumn = 2;
    public static readonly StyledProperty<Control?> MasterProperty = AvaloniaProperty.Register<AdaptivePane, Control?>(
        nameof(Master)
    );
    public static readonly StyledProperty<Control?> DetailProperty = AvaloniaProperty.Register<AdaptivePane, Control?>(
        nameof(Detail)
    );
    public static readonly StyledProperty<bool> DetailOpenProperty = AvaloniaProperty.Register<AdaptivePane, bool>(
        nameof(DetailOpen)
    );
    public static readonly StyledProperty<string> BackLabelProperty = AvaloniaProperty.Register<AdaptivePane, string>(
        nameof(BackLabel),
        "Back to list"
    );

    public Control? Master
    {
        get => GetValue(MasterProperty);
        set => SetValue(MasterProperty, value);
    }
    public Control? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }
    public bool DetailOpen
    {
        get => GetValue(DetailOpenProperty);
        set => SetValue(DetailOpenProperty, value);
    }
    public string BackLabel
    {
        get => GetValue(BackLabelProperty);
        set => SetValue(BackLabelProperty, value);
    }
    public bool IsCompact => Bounds.Width <= CompactWidth;

    public AdaptivePane()
    {
        InitializeComponent();
        MasterHost.AddHandler(KeyDownEvent, MasterKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        SizeChanged += (_, _) => ArrangePanes();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DetailOpenProperty)
            ArrangePanes();
    }

    private void ArrangePanes()
    {
        Columns.ColumnDefinitions[0].Width = IsCompact
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(MasterWidth);
        Columns.ColumnDefinitions[1].Width = new GridLength(IsCompact ? 0 : 1);
        Columns.ColumnDefinitions[DetailColumn].Width = IsCompact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(DetailHost, IsCompact ? 0 : DetailColumn);
        MasterHost.IsVisible = !IsCompact || !DetailOpen;
        DetailHost.IsVisible = !IsCompact || DetailOpen;
        Divider.IsVisible = !IsCompact;
        Back.IsVisible = IsCompact;
    }

    private void MasterTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Control control)
            return;
        var item = control as ListBoxItem ?? control.FindAncestorOfType<ListBoxItem>();
        if (item?.FindAncestorOfType<ListBox>() != null)
            SetCurrentValue(DetailOpenProperty, true);
    }

    private void MasterKeyDown(object? sender, KeyEventArgs e)
    {
        if (
            e.Key == Key.Enter
            && e.Source is Control control
            && (control as ListBox ?? control.FindAncestorOfType<ListBox>())?.SelectedItem != null
        )
        {
            SetCurrentValue(DetailOpenProperty, true);
            e.Handled = true;
        }
    }

    private void BackClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(DetailOpenProperty, false);
        var list = MasterHost.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!DetailOpen && list?.SelectedItem is { } selected)
                    list.ContainerFromItem(selected)?.Focus();
            },
            DispatcherPriority.Loaded
        );
    }
}
