namespace UiComparison;

public sealed record Article(int Id, string Feed, string Title, string Body)
{
    public string Caption => $"{Title}\n{Feed} · 17 Nov 2026";
}
