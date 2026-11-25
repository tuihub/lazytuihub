using LazyTuiHub.Plugin.TuiHub.Articles;
using LazyTuiHub.Plugin.TuiHub.Rss.Frontend;

namespace LazyTuiHub.Plugin.TuiHub.Rss;

internal sealed class RssArticleSources(FeedOperations feeds) : IArticleSources
{
    public async Task<IReadOnlyList<ArticleSource>> ListAsync(CancellationToken token)
    {
        var result = new List<ArticleSource>();
        var next = string.Empty;
        do
        {
            var page = await feeds.Feeds(token, next);
            result.AddRange(
                page.Feeds.Select(feed => new ArticleSource(
                    feed.Name,
                    string.IsNullOrEmpty(feed.DisplayName) ? feed.Uri : feed.DisplayName
                ))
            );
            next = page.NextPageToken;
        } while (!string.IsNullOrEmpty(next));
        return result;
    }

    public async Task<string> SubscribeAsync(string url, string title, CancellationToken token) =>
        (await feeds.Add(url, token, title)).Name;

    public async Task<int> RefreshAsync(string source, CancellationToken token) =>
        (await feeds.Refresh(source, token)).ProcessedArticleCount;
}
