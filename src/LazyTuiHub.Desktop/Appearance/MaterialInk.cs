using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace LazyTuiHub.Desktop;

public sealed class MaterialInk : Control
{
    public static readonly StyledProperty<bool> PressedProperty = AvaloniaProperty.Register<MaterialInk, bool>(
        nameof(Pressed)
    );

    private const double PressedOpacity = 0.12;
    private static readonly TimeSpan Expansion = TimeSpan.FromMilliseconds(225);
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(150);
    private readonly Stopwatch elapsed = new();
    private Point? pointer;
    private Point origin;
    private TimeSpan? release;
    private bool framePending;

    public bool Pressed
    {
        get => GetValue(PressedProperty);
        set => SetValue(PressedProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TemplatedParent is Button button)
            button.AddHandler(PointerPressedEvent, RememberPointer, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TemplatedParent is Button button)
            button.RemoveHandler(PointerPressedEvent, RememberPointer);
        elapsed.Reset();
        pointer = null;
        framePending = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void RememberPointer(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            pointer = e.GetPosition(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PressedProperty)
            return;
        if (Pressed)
        {
            origin = pointer ?? new Rect(Bounds.Size).Center;
            pointer = null;
            elapsed.Restart();
            release = null;
        }
        else
            release = elapsed.Elapsed;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!elapsed.IsRunning)
            return;
        var now = elapsed.Elapsed;
        var opacity = release is { } end ? Math.Clamp(1 - (now - end) / Fade, 0, 1) : 1;
        if (opacity <= 0)
        {
            elapsed.Reset();
            return;
        }
        var dx = Math.Max(origin.X, Bounds.Width - origin.X);
        var dy = Math.Max(origin.Y, Bounds.Height - origin.Y);
        var progress = Math.Clamp(now / Expansion, 0, 1);
        var radius = Math.Sqrt(dx * dx + dy * dy) * (1 - Math.Pow(1 - progress, 2));
        using (context.PushOpacity(PressedOpacity * opacity))
            context.DrawEllipse(TextElement.GetForeground(this), null, origin, radius, radius);
        if (!framePending && (progress < 1 || release.HasValue) && TopLevel.GetTopLevel(this) is { } root)
        {
            framePending = true;
            root.RequestAnimationFrame(Redraw);
        }
    }

    private void Redraw(TimeSpan time)
    {
        framePending = false;
        InvalidateVisual();
    }
}
