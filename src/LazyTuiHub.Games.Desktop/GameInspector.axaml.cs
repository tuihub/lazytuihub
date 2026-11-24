using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LazyTuiHub.Games.Desktop;

public sealed partial class GameInspector : UserControl
{
    private GamePageModel Model => (GamePageModel)DataContext!;

    public GameInspector()
    {
        InitializeComponent();
    }

    internal void SetSplit(bool split)
    {
        Back.IsVisible = !split;
        CloseDetail.IsVisible = split;
    }

    internal void ScrollToTop() => Body.ScrollToHome();

    private void BackClick(object? sender, RoutedEventArgs args) => Model.Back();

    private async void IdentifyClick(object? sender, RoutedEventArgs args) => await Model.IdentifyAsync();

    private void EditClick(object? sender, RoutedEventArgs args) => Model.StartEdit();

    private async void FillClick(object? sender, RoutedEventArgs args) => await Model.FillAsync();

    private void Reassign(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: GameAvailability item })
            Model.StartMove(item);
    }
}
