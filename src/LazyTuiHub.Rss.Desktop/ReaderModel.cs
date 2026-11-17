using System.Collections.ObjectModel;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Desktop;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Rss.Desktop;

public sealed class ReaderModel(IRssApi api, DesktopSession session, ILogger<ReaderModel> logger)
    : PageModel(session, logger)
{
    private const string AllFeeds = "feeds/-";
    private Article? selectedArticle;
    private string nextPage = string.Empty;
    public ObservableCollection<FeedChoice> Feeds { get; } = [];
    public ObservableCollection<Article> Articles { get; } = [];
    public FeedChoice? SelectedFeed { get; private set; }
    public string FeedUri { get; set; } = string.Empty;
    public string FeedTitle { get; set; } = string.Empty;
    public string Filter { get; private set; } = string.Empty;
    public bool HasMore => !string.IsNullOrEmpty(nextPage);
    public bool CanRefresh => SelectedFeed is { Name: not AllFeeds };
    public bool CanMarkRead => SelectedArticle is { Read: false };
    public Article? SelectedArticle
    {
        get => selectedArticle;
        set
        {
            selectedArticle = value;
            Changed();
        }
    }

    public Task LoadAsync() =>
        RunAsync(async token =>
        {
            await LoadFeedsAsync(SelectedFeed?.Name, token);
            await LoadArticlesAsync(false, token);
        });

    public Task SubscribeAsync() =>
        RunAsync(async token =>
        {
            var request = new CreateFeedRequest
            {
                Feed = new() { Uri = FeedUri, DisplayName = FeedTitle },
                RequestId = Guid.NewGuid().ToString("D"),
            };
            var feed = await Task.Run(() => api.CreateFeedAsync(request, token), token);
            FeedUri = string.Empty;
            FeedTitle = string.Empty;
            await LoadFeedsAsync(feed.Name, token);
            await LoadArticlesAsync(false, token);
            Status = "Subscription added. Choose Fetch to retrieve its articles.";
        });

    public Task SelectFeedAsync(FeedChoice feed) =>
        RunAsync(async token =>
        {
            SelectedFeed = feed;
            await LoadArticlesAsync(false, token);
        });

    public Task SetFilterAsync(string filter) =>
        RunAsync(async token =>
        {
            Filter = filter;
            await LoadArticlesAsync(false, token);
        });

    public Task MoreAsync() => RunAsync(token => LoadArticlesAsync(true, token));

    public Task RefreshAsync() =>
        RunAsync(async token =>
        {
            if (!CanRefresh)
                return;
            var result = await Task.Run(() => api.RefreshFeedAsync(new() { Name = SelectedFeed!.Name }, token), token);
            await LoadArticlesAsync(false, token);
            Status = $"Fetched {result.ProcessedArticleCount} articles. {Status}";
        });

    public Task MarkReadAsync() =>
        RunAsync(async token =>
        {
            if (SelectedArticle is not { Read: false } article)
                return;
            var updated = await Task.Run(() => api.MarkArticleReadAsync(new() { Name = article.Name }, token), token);
            var index = Articles.IndexOf(article);
            if (string.Equals(Filter, "read = false", StringComparison.Ordinal))
            {
                Articles.RemoveAt(index);
                SelectedArticle = Articles.FirstOrDefault();
            }
            else
            {
                Articles[index] = updated;
                SelectedArticle = updated;
            }
            Status = "Marked as read.";
        });

    private async Task LoadFeedsAsync(string? selected, CancellationToken token)
    {
        var feeds = await Task.Run(
            async () =>
            {
                var result = new List<FeedChoice> { new(AllFeeds, "All feeds") };
                var request = new ListFeedsRequest();
                do
                {
                    var page = await api.ListFeedsAsync(request, token);
                    result.AddRange(
                        page.Feeds.Select(feed => new FeedChoice(
                            feed.Name,
                            string.IsNullOrEmpty(feed.DisplayName) ? feed.Uri : feed.DisplayName
                        ))
                    );
                    request.PageToken = page.NextPageToken;
                } while (!string.IsNullOrEmpty(request.PageToken));
                return result;
            },
            token
        );
        Feeds.Clear();
        foreach (var feed in feeds)
            Feeds.Add(feed);
        SelectedFeed = Feeds.FirstOrDefault(feed => string.Equals(feed.Name, selected, StringComparison.Ordinal))
            ?? Feeds[0];
    }

    private async Task LoadArticlesAsync(bool append, CancellationToken token)
    {
        if (append && !HasMore)
            return;
        if (!append)
        {
            nextPage = string.Empty;
            Articles.Clear();
            SelectedArticle = null;
        }
        var request = new ListArticlesRequest
        {
            Parent = SelectedFeed?.Name ?? AllFeeds,
            Filter = Filter,
            PageToken = append ? nextPage : string.Empty,
        };
        var page = await Task.Run(() => api.ListArticlesAsync(request, token), token);
        foreach (var article in page.Articles)
            Articles.Add(article);
        nextPage = page.NextPageToken;
        SelectedArticle ??= Articles.FirstOrDefault();
        Status =
            Articles.Count == 0
                ? "No articles. Add a subscription or fetch a selected feed."
                : $"{Articles.Count} articles loaded.";
    }
}
