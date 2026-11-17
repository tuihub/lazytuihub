using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Uno.Toolkit.UI;
using Windows.Foundation;
using ConnectionPath = Microsoft.UI.Xaml.Shapes.Path;

namespace UiComparison.UnoApp;

public sealed class WorkflowView : Grid
{
    private readonly Canvas canvas = new() { Width = 1100, Height = 600 };
    private readonly GraphNode[] nodes = Graph.Create();
    private readonly List<ConnectionPath> edges = [];
    private readonly ScaleTransform scale = new() { ScaleX = 1, ScaleY = 1 };
    private readonly TextBlock zoom = ReaderView.Text("100%", 14);
    private readonly Border surface;

    public WorkflowView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var toolbar = new FlexPanel
        {
            Margin = new Thickness(20),
            ColumnGap = 12,
            RowGap = 8,
            Wrap = FlexWrap.Wrap,
        };
        toolbar.Children.Add(ReaderView.Text("RSS / WORKFLOW", 20));
        toolbar.Children.Add(ReaderView.Action("−", () => SetZoom(scale.ScaleX / 1.15)));
        toolbar.Children.Add(zoom);
        toolbar.Children.Add(ReaderView.Action("+", () => SetZoom(scale.ScaleX * 1.15)));
        toolbar.Children.Add(ReaderView.Action("Reset", () => SetZoom(1)));
        toolbar.Children.Add(ReaderView.Text("Drag nodes · Scroll to pan", 14));
        Children.Add(toolbar);
        foreach (var edge in Graph.Edges)
        {
            var path = new ConnectionPath { Stroke = new SolidColorBrush(Colors.SteelBlue), StrokeThickness = 3 };
            edges.Add(path);
            canvas.Children.Add(path);
        }
        foreach (var node in nodes)
            AddNode(node);
        canvas.RenderTransform = scale;
        surface = new Border
        {
            Width = 1100,
            Height = 600,
            Child = canvas,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var scroll = new ScrollViewer
        {
            Content = surface,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
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
            Background = new SolidColorBrush(Colors.SteelBlue),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = node.Title,
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 18,
                    },
                    new TextBlock
                    {
                        Text = node.Detail,
                        Foreground = new SolidColorBrush(Colors.White),
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
            previous = e.GetCurrentPoint(canvas).Position;
            card.CapturePointer(e.Pointer);
            e.Handled = true;
        };
        card.PointerMoved += (_, e) =>
        {
            if (previous is not { } last)
                return;
            var point = e.GetCurrentPoint(canvas).Position;
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
            card.ReleasePointerCapture(e.Pointer);
        };
        card.PointerCaptureLost += (_, _) => previous = null;
    }

    private void SetZoom(double value)
    {
        scale.ScaleX = value;
        scale.ScaleY = value;
        surface.Width = 1100 * value;
        surface.Height = 600 * value;
        zoom.Text = $"{value:P0}";
    }

    private void UpdateEdges()
    {
        for (var index = 0; index < Graph.Edges.Length; index++)
        {
            var (from, to) = Graph.Edges[index];
            var a = nodes[from];
            var b = nodes[to];
            edges[index].Data = new PathGeometry
            {
                Figures =
                {
                    new PathFigure
                    {
                        StartPoint = new Point(a.X + 170, a.Y + 45),
                        Segments =
                        {
                            new BezierSegment
                            {
                                Point1 = new Point(a.X + 220, a.Y + 45),
                                Point2 = new Point(b.X - 50, b.Y + 45),
                                Point3 = new Point(b.X, b.Y + 45),
                            },
                        },
                    },
                },
            };
        }
    }

    public void Verify()
    {
        var panel = new FlexPanel { Wrap = FlexWrap.Wrap };
        for (var index = 0; index < 3; index++)
            panel.Children.Add(new Border { Width = 60, Height = 20 });
        panel.Measure(new Size(140, 100));
        panel.Arrange(new Rect(0, 0, 140, 100));
        if (
            Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot((FrameworkElement)panel.Children[2]).Y
                <= 0
            || edges.Count != 4
        )
            throw new InvalidOperationException("Flex/graph failed.");
        var grow = new FlexPanel();
        var first = new Border { Height = 20 };
        var second = new Border { Height = 20 };
        FlexPanel.SetBasis(first, 50);
        FlexPanel.SetBasis(second, 50);
        FlexPanel.SetGrow(second, 1);
        grow.Children.Add(first);
        grow.Children.Add(second);
        grow.Measure(new Size(200, 50));
        grow.Arrange(new Rect(0, 0, 200, 50));
        if (Math.Abs(first.ActualWidth - 50) > 0.01 || Math.Abs(second.ActualWidth - 150) > 0.01)
            throw new InvalidOperationException("Flex grow failed.");
        SetZoom(1.15);
        if (Math.Abs(scale.ScaleX - 1.15) > 0.0001)
            throw new InvalidOperationException("Zoom failed.");
        SetZoom(1);
    }
}
