namespace LazyTuiHub.Plugin.TuiHub.Articles;

public interface IArticleSources
{
    Task<IReadOnlyList<ArticleSource>> ListAsync(CancellationToken token);
    Task<string> SubscribeAsync(string url, string title, CancellationToken token);
    Task<int> RefreshAsync(string source, CancellationToken token);
}
