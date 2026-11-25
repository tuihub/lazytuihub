using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace LazyTuiHub.Games.Desktop;

public sealed partial class GamePoster : UserControl
{
    internal const double CoverAspect = 1.5;
    private const int InkShades = 5;
    private const double FirstTint = 0.14,
        TintStep = 0.04;
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<GamePoster, string?>(
        nameof(Title)
    );
    public static readonly StyledProperty<string?> UrlProperty = AvaloniaProperty.Register<GamePoster, string?>(
        nameof(Url)
    );
    public static readonly StyledProperty<string?> IdentityProperty = AvaloniaProperty.Register<GamePoster, string?>(
        nameof(Identity)
    );
    public string? Identity
    {
        get => GetValue(IdentityProperty);
        set => SetValue(IdentityProperty, value);
    }
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public GamePoster()
    {
        InitializeComponent();
        PropertyChanged += (_, change) =>
        {
            if (change.Property == TitleProperty || change.Property == IdentityProperty)
                SetLettering();
        };
        SetLettering();
    }

    private void SetLettering()
    {
        var title = Title?.Trim() ?? string.Empty;
        var elements = StringInfo.GetTextElementEnumerator(title);
        Monogram.Text = elements.MoveNext() ? elements.GetTextElement().ToUpperInvariant() : string.Empty;
        // Keep the printed cover recognizable across sessions, including for non-Latin titles.
        var color = 0;
        foreach (var character in Identity ?? title)
            color = (color + character) % InkShades;
        Paper.Opacity = FirstTint + color * TintStep;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsFinite(availableSize.Width))
            return base.MeasureOverride(availableSize);
        var size = new Size(availableSize.Width, Math.Min(availableSize.Height, availableSize.Width * CoverAspect));
        base.MeasureOverride(size);
        return size;
    }
}
