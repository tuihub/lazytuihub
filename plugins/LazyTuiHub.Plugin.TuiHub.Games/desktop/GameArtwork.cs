using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace LazyTuiHub.Games.Desktop;

public sealed class GameArtwork : Image
{
    public static readonly StyledProperty<string?> UrlProperty = AvaloniaProperty.Register<GameArtwork, string?>(
        nameof(Url)
    );
    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }
    private CancellationTokenSource? pending;
    private Bitmap? image;

    public GameArtwork()
    {
        Loaded += async (_, _) => await LoadAsync();
        Unloaded += (_, _) => Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UrlProperty && IsLoaded)
            _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Clear();
        if (
            this.FindAncestorOfType<GamePage>() is not { } page
            || !Uri.TryCreate(Url, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
            return;
        using var request = new CancellationTokenSource();
        pending = request;
        try
        {
            var bytes = await page.Covers.ReadAsync(uri, request.Token);
            using var buffer = new MemoryStream(bytes, writable: false);
            const int coverWidth = 640;
            var loaded = Bitmap.DecodeToWidth(buffer, coverWidth);
            if (request.IsCancellationRequested)
                loaded.Dispose();
            else
            {
                image = loaded;
                Source = image;
                IsVisible = true;
            }
        }
        catch (Exception error)
            when (error is HttpRequestException or OperationCanceledException or ArgumentException or IOException)
        {
            // Browsing and editing remain available when the cover cannot be loaded.
        }
        finally
        {
            if (ReferenceEquals(pending, request))
                pending = null;
        }
    }

    private void Clear()
    {
        pending?.Cancel();
        pending = null;
        Source = null;
        IsVisible = false;
        image?.Dispose();
        image = null;
    }
}
