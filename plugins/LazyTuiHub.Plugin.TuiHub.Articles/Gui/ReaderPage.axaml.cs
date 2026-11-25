using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LazyTuiHub.Sdk.Avalonia;

namespace LazyTuiHub.Plugin.TuiHub.Articles.Desktop;

internal sealed partial class ReaderPage : UserControl, IWorkspaceView
{
    private bool ready;
    private string? displayedArticle;
    public ReaderModel Model { get; }

    public Task RefreshAsync(IReadOnlySet<string> changedGroups) =>
        changedGroups.Contains("articles") || changedGroups.Contains("rss") ? Model.LoadAsync() : Task.CompletedTask;

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

    private void ArticleChanged(object? sender, SelectionChangedEventArgs e)
    {
        var name = Model.SelectedArticle?.Name;
        if (string.Equals(displayedArticle, name, StringComparison.Ordinal))
            return;
        displayedArticle = name;
        ArticleScroll.Offset = default;
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

    private void AddFeedClick(object? sender, RoutedEventArgs e)
    {
        Model.AddingSubscription = true;
        Dispatcher.UIThread.Post(() => FeedUriInput.Focus(), DispatcherPriority.Loaded);
    }

    private void CloseSubscriptionClick(object? sender, RoutedEventArgs e)
    {
        Model.AddingSubscription = false;
        FocusLibraryAction();
    }

    private async void SubscribeClick(object? sender, RoutedEventArgs e)
    {
        await Model.SubscribeAsync();
        if (!Model.AddingSubscription)
            FocusLibraryAction();
    }

    private void FocusLibraryAction() =>
        Dispatcher.UIThread.Post(() => AddFeedButton.Focus(), DispatcherPriority.Loaded);

    private async void RefreshClick(object? sender, RoutedEventArgs e) =>
        await (Model.CanRefresh ? Model.RefreshAsync() : Model.LoadAsync());

    private async void ReloadClick(object? sender, RoutedEventArgs e) => await Model.LoadAsync();

    private async void MoreClick(object? sender, RoutedEventArgs e) => await Model.MoreAsync();

    private async void ReadClick(object? sender, RoutedEventArgs e) => await Model.MarkReadAsync();
}
