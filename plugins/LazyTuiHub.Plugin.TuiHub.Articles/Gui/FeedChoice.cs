namespace LazyTuiHub.Plugin.TuiHub.Articles.Desktop;

internal sealed record FeedChoice(string Name, string Title)
{
    public override string ToString() => Title;
}
