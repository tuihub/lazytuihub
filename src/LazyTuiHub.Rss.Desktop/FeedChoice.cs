namespace LazyTuiHub.Rss.Desktop;

public sealed record FeedChoice(string Name, string Title)
{
    public override string ToString() => Title;
}
