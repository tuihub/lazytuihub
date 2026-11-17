using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Labs.Panels;
using Avalonia.Layout;
using Avalonia.Media;
using ConnectionPath = Avalonia.Controls.Shapes.Path;

namespace UiComparison.AvaloniaApp;

public sealed class WorkflowView : Grid
{
    private readonly Canvas canvas = new() { Width = 1100, Height = 600 };
    private readonly GraphNode[] nodes = Graph.Create();
    private readonly List<ConnectionPath> edges = [];
    private readonly ScaleTransform scale = new(1, 1);
    private readonly TextBlock zoom = ReaderWindow.Text("100%", 14);

    public WorkflowView()
    {
        RowDefinitions = new RowDefinitions("Auto,*");
        var toolbar = new FlexPanel
        {
            Margin = new Thickness(20),
            ColumnSpacing = 12,
            RowSpacing = 8,
            Wrap = FlexWrap.Wrap,
        };
        toolbar.Children.Add(ReaderWindow.Text("RSS / WORKFLOW", 20));
        toolbar.Children.Add(ReaderWindow.Action("−", () => SetZoom(scale.ScaleX / 1.15)));
        toolbar.Children.Add(zoom);
        toolbar.Children.Add(ReaderWindow.Action("+", () => SetZoom(scale.ScaleX * 1.15)));
        toolbar.Children.Add(ReaderWindow.Action("Reset", () => SetZoom(1)));
        toolbar.Children.Add(ReaderWindow.Text("Drag nodes · Scroll to pan", 14));
        Children.Add(toolbar);
        foreach (var edge in Graph.Edges)
        {
            var path = new ConnectionPath { Stroke = Brushes.SteelBlue, StrokeThickness = 3 };
            edges.Add(path);
            canvas.Children.Add(path);
        }
        foreach (var node in nodes)
            AddNode(node);
        var transformed = new LayoutTransformControl { LayoutTransform = scale, Child = canvas };
        var scroll = new ScrollViewer
        {
            Content = transformed,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        Grid.SetRow(scroll, 1);
        Children.Add(scroll);
        UpdateEdges();
    }

    private void AddNode(GraphNode node)
    {
        var card = new Border
        {
            Width = 170,
            Height = 90,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.SteelBlue,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = node.Title,
                        Foreground = Brushes.White,
                        FontSize = 18,
                    },
                    new TextBlock
                    {
                        Text = node.Detail,
                        Foreground = Brushes.White,
                        FontSize = 12,
                    },
                },
            },
        };
        Canvas.SetLeft(card, node.X);
        Canvas.SetTop(card, node.Y);
        canvas.Children.Add(card);
        Point? previous = null;
        card.PointerPressed += (_, e) =>
        {
            previous = e.GetPosition(canvas);
            e.Pointer.Capture(card);
            e.Handled = true;
        };
        card.PointerMoved += (_, e) =>
        {
            if (previous is not { } last)
                return;
            var point = e.GetPosition(canvas);
            node.X += point.X - last.X;
            node.Y += point.Y - last.Y;
            previous = point;
            Canvas.SetLeft(card, node.X);
            Canvas.SetTop(card, node.Y);
            UpdateEdges();
        };
        card.PointerReleased += (_, e) =>
        {
            previous = null;
            e.Pointer.Capture(null);
        };
        card.PointerCaptureLost += (_, _) => previous = null;
    }

    private void SetZoom(double value)
    {
        scale.ScaleX = value;
        scale.ScaleY = value;
        zoom.Text = $"{value:P0}";
    }

    private void UpdateEdges()
    {
        for (var index = 0; index < Graph.Edges.Length; index++)
        {
            var (from, to) = Graph.Edges[index];
            var a = nodes[from];
            var b = nodes[to];
            edges[index].Data = Geometry.Parse(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"M {a.X + 170},{a.Y + 45} C {a.X + 220},{a.Y + 45} {b.X - 50},{b.Y + 45} {b.X},{b.Y + 45}"
                )
            );
        }
    }

    public void Verify()
    {
        var panel = new FlexPanel { Wrap = FlexWrap.Wrap };
        for (var index = 0; index < 3; index++)
            panel.Children.Add(new Border { Width = 60, Height = 20 });
        panel.Measure(new Size(140, 100));
        panel.Arrange(new Rect(0, 0, 140, 100));
        if (panel.Children[2].Bounds.Y <= 0 || edges.Count != 4)
            throw new InvalidOperationException("Flex/graph failed.");
        var grow = new FlexPanel();
        var first = new Border { Height = 20 };
        var second = new Border { Height = 20 };
        Flex.SetBasis(first, FlexBasis.Parse("50"));
        Flex.SetBasis(second, FlexBasis.Parse("50"));
        Flex.SetGrow(second, 1);
        grow.Children.Add(first);
        grow.Children.Add(second);
        grow.Measure(new Size(200, 50));
        grow.Arrange(new Rect(0, 0, 200, 50));
        if (Math.Abs(first.Bounds.Width - 50) > 0.01 || Math.Abs(second.Bounds.Width - 150) > 0.01)
            throw new InvalidOperationException("Flex grow failed.");
        SetZoom(1.15);
        if (Math.Abs(scale.ScaleX - 1.15) > 0.0001)
            throw new InvalidOperationException("Zoom failed.");
        SetZoom(1);
    }
}
