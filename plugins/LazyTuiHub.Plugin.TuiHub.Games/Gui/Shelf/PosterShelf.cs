using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace LazyTuiHub.Plugin.TuiHub.Games.Desktop;

internal sealed class PosterShelf : Decorator
{
    internal const double TilePadding = 6;
    private const double TextSpacing = 10;
    private const int Pair = 2;
    private const double CompactPairWidth = 300;

    internal int Columns { get; private set; } = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (
            Child is ItemsRepeater { Layout: UniformGridLayout grid }
            && double.IsFinite(availableSize.Width)
            && availableSize.Width > 0
        )
            SizeCells(grid, availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    private void SizeCells(UniformGridLayout grid, double width)
    {
        var minimum = (double)this.FindResource("PosterMinWidth")!;
        Columns = Math.Max(1, (int)Math.Floor((width + grid.MinColumnSpacing) / (minimum + grid.MinColumnSpacing)));
        if (Columns == 1 && width >= CompactPairWidth)
            Columns = Pair;
        var cell = (width - (Columns - 1) * grid.MinColumnSpacing) / Columns;
        var inset = Pair * (TilePadding + ((Thickness)this.FindResource("TileFrameWidth")!).Top);
        var title = 2 * (double)this.FindResource("ItemTitleLineHeight")!;
        var caption = (double)this.FindResource("MetadataLineHeight")!;
        grid.MinItemWidth = Math.Max(1, Math.Floor(width / Columns) - grid.MinColumnSpacing);
        grid.MaximumRowsOrColumns = Columns;
        grid.MinItemHeight = Math.Ceiling(
            Math.Max(0, cell - inset) * GamePoster.CoverAspect + inset + title + caption + TextSpacing
        );
    }
}
