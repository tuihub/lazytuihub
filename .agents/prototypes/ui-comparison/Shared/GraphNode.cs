namespace UiComparison;

public sealed class GraphNode(string title, string detail, double x, double y)
{
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}
