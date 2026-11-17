using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyTuiHub.Rss.Desktop;

public sealed partial class ReaderPage : UserControl
{
    private bool ready;
    public ReaderModel Model { get; }

    public ReaderPage(ReaderModel model)
    {
        Model = model;
        InitializeComponent();
        DataContext = model;
        ready = true;
        Loaded += PageLoaded;
    }

    private async void PageLoaded(object? sender, RoutedEventArgs e)
    {
        await Model.InitializeAsync();
    }

    private async void FeedChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ready && !Model.Busy && FeedPicker.SelectedItem is FeedChoice feed && feed != Model.SelectedFeed)
            await Model.SelectFeedAsync(feed);
    }

    private async void FilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!ready || Model.Busy)
            return;
        if (
            ReadFilter.SelectedItem is ComboBoxItem { Tag: string filter }
            && !string.Equals(filter, Model.Filter, StringComparison.Ordinal)
        )
            await Model.SetFilterAsync(filter);
    }

    private async void SubscribeClick(object? sender, RoutedEventArgs e) => await Model.SubscribeAsync();

    private async void FetchClick(object? sender, RoutedEventArgs e) => await Model.RefreshAsync();

    private async void ReloadClick(object? sender, RoutedEventArgs e) => await Model.LoadAsync();

    private async void MoreClick(object? sender, RoutedEventArgs e) => await Model.MoreAsync();

    private async void ReadClick(object? sender, RoutedEventArgs e) => await Model.MarkReadAsync();
}
