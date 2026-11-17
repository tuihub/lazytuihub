using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyTuiHub.Rss.Desktop;

public sealed partial class ReaderPage : UserControl
{
    private bool loaded;
    public ReaderModel Model { get; }

    public ReaderPage(ReaderModel model)
    {
        Model = model;
        InitializeComponent();
        DataContext = model;
        Loaded += PageLoaded;
    }

    private async void PageLoaded(object? sender, RoutedEventArgs e)
    {
        if (loaded)
            return;
        loaded = true;
        await Model.LoadAsync();
    }

    private async void FeedChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Model.Busy && FeedPicker.SelectedItem is FeedChoice feed && feed != Model.SelectedFeed)
            await Model.SelectFeedAsync(feed);
    }

    private async void FilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!loaded || Model.Busy)
            return;
        if (ReadFilter.SelectedItem is ComboBoxItem { Tag: string filter })
            await Model.SetFilterAsync(filter);
    }

    private async void SubscribeClick(object? sender, RoutedEventArgs e) => await Model.SubscribeAsync();

    private async void FetchClick(object? sender, RoutedEventArgs e) => await Model.RefreshAsync();

    private async void ReloadClick(object? sender, RoutedEventArgs e) => await Model.LoadAsync();

    private async void MoreClick(object? sender, RoutedEventArgs e) => await Model.MoreAsync();

    private async void ReadClick(object? sender, RoutedEventArgs e) => await Model.MarkReadAsync();
}
