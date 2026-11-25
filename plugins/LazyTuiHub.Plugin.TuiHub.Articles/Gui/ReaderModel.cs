using System.Collections.ObjectModel;
using System.Globalization;
using LazyTuiHub.Api.Articles.V1;
using LazyTuiHub.Plugin.TuiHub.Articles.Frontend;
using LazyTuiHub.Sdk.Avalonia;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Plugin.TuiHub.Articles.Desktop;

internal sealed class ReaderModel(
    ArticleOperations articles,
    RunDesktopOperation run,
    ILogger<ReaderModel> logger,
    IArticleSources? subscriptions = null
) : PageModel(run, logger)
{
    private const string AllSources = "";
    public bool CanSubscribe => subscriptions is not null;
    private Article? selectedArticle;
    private string nextPage = string.Empty;
    private bool initialized;
    private bool addingSubscription;
    public ObservableCollection<FeedChoice> Feeds { get; } = [];
    public ObservableCollection<Article> Articles { get; } = [];
    public FeedChoice? SelectedFeed { get; private set; }
    public string FeedUri { get; set; } = string.Empty;
    public string FeedTitle { get; set; } = string.Empty;
    public string Filter { get; private set; } = string.Empty;
    public bool AddingSubscription
    {
        get => addingSubscription;
        set
        {
            addingSubscription = value;
            Changed();
        }
    }
    public bool DetailOpen { get; set; }
    public string ArticleCount =>
        (Articles.Count, HasMore) switch
        {
            (var count, true) => $"{count} articles loaded",
            (1, false) => "1 article",
            (var count, false) => $"{count} articles",
        };
    public string RefreshLabel => CanRefresh ? "Fetch latest articles" : "Reload saved articles";
    public string EmptyMessage =>
        Filter switch
        {
            "read = false" => "No unread articles here. Choose another source or show all articles.",
            "read = true" => "Articles you mark as read will appear here.",
            _ when CanRefresh => "Use refresh to fetch the latest articles from this feed.",
            _ when Feeds.Count > 1 => "Choose a subscription and refresh it to start reading.",
            _ when !CanSubscribe => "Articles collected from your sources will appear here.",
            _ => "Add a subscription to collect your favorite reads here.",
        };
    public bool HasArticles => Articles.Count > 0;
    public bool HasSelection => SelectedArticle != null;
    public string PublishedAt =>
        SelectedArticle?.PublishTime?.ToDateTime().ToLocalTime().ToString("f", CultureInfo.CurrentCulture)
        ?? string.Empty;
    public bool HasMore => !string.IsNullOrEmpty(nextPage);
    public bool CanRefresh => SelectedFeed is { Name: not AllSources };
    public bool CanMarkRead => Idle && SelectedArticle is { Read: false };
    public Article? SelectedArticle
    {
        get => selectedArticle;
        set
        {
            selectedArticle = value;
            Changed();
        }
    }

    public Task InitializeAsync()
    {
        if (initialized)
            return Task.CompletedTask;
        initialized = true;
        return LoadAsync();
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
            if (subscriptions is null)
                return;
            var name = await Task.Run(() => subscriptions.SubscribeAsync(FeedUri, FeedTitle, token), token);
            FeedUri = string.Empty;
            FeedTitle = string.Empty;
            await LoadFeedsAsync(name, token);
            await LoadArticlesAsync(false, token);
            AddingSubscription = false;
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
            if (subscriptions is null || !CanRefresh)
                return;
            var result = await Task.Run(() => subscriptions.RefreshAsync(SelectedFeed!.Name, token), token);
            await LoadArticlesAsync(false, token);
            Status = $"Fetched {result} articles. {Status}";
        });

    public Task MarkReadAsync() =>
        RunAsync(async token =>
        {
            if (SelectedArticle is not { Read: false } article)
                return;
            var updated = await Task.Run(() => articles.Read(article.Name, token), token);
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
                var result = new List<FeedChoice> { new(AllSources, "All sources") };
                if (subscriptions is null)
                    return result;
                var sources = await subscriptions.ListAsync(token);
                result.AddRange(sources.Select(source => new FeedChoice(source.Name, source.Title)));
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
            DetailOpen = false;
            nextPage = string.Empty;
            Articles.Clear();
            SelectedArticle = null;
        }
        var page = await Task.Run(
            () => articles.List(token, SelectedFeed?.Name, Filter, pageToken: append ? nextPage : string.Empty),
            token
        );
        foreach (var article in page.Articles)
            Articles.Add(article);
        nextPage = page.NextPageToken;
        SelectedArticle ??= Articles.FirstOrDefault();
        Status = Articles.Count == 0 ? "No articles match this selection." : $"{Articles.Count} articles loaded.";
    }
}
