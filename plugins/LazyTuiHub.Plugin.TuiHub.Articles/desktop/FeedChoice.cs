namespace LazyTuiHub.Articles.Desktop;

public sealed record FeedChoice(string Name, string Title)
{
    public override string ToString() => Title;
}
