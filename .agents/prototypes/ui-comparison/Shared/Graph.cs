namespace UiComparison;

public static class Graph
{
    public static GraphNode[] Create() =>
        [
            new("Timer", "Every 15 minutes", 40, 130),
            new("HTTP fetch", "Stream · document", 260, 130),
            new("RSS parser", "Stream · article", 480, 130),
            new("SQLite", "Business storage", 720, 55),
            new("Distribute", "Cross-business branch", 720, 245),
        ];

    public static readonly (int From, int To)[] Edges = [(0, 1), (1, 2), (2, 3), (2, 4)];
}
