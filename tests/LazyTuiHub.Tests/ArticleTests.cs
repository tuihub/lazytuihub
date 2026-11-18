using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Articles;
using LazyTuiHub.Articles;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class ArticleTests
{
    [Fact]
    public async Task IndependentSourceWritesThroughTheArticleNodeWithRssDisabled()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using (var initial = PluginTestHost.Create(workspace.Database, http))
            initial.Services.GetRequiredService<PluginRuntime>().SetEnabled("rss", false);
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var workflows = app.Services.GetRequiredService<WorkflowRuntime>();
        var api = app.Services.GetRequiredService<IArticleApi>();
        var token = TestContext.Current.CancellationToken;
        StreamNodes.Source<ArticleDraft>(workflows.Registry, "notes.emit", "notes", (context, _) =>
            new[] { new DataPacket<ArticleDraft>((ArticleDraft)context.Run.Input!) }.ToAsyncEnumerable()
        );
        workflows.Save(
            new(
                "notes.import",
                [PipelineNode.Create("source", "notes.emit"), PipelineNode.Create("save", "articles.store")],
                [new("source", StreamNodes.Output, "save", StreamNodes.Input)]
            )
        );
        var first = new ArticleDraft("notebooks/a", "1", "Alpha", "", "Body", null);
        await workflows.RunAsync("notes.import", new(first), token);
        await workflows.RunAsync("notes.import", new(first with { Source = "notebooks/b", Title = "Beta" }), token);
        var all = await api.ListArticlesAsync(new(), token);
        Assert.Equal(2, all.Articles.Count);
        var article = Assert.Single(all.Articles, item => item.Source == first.Source);
        await api.MarkArticleReadAsync(new() { Name = article.Name }, token);
        await workflows.RunAsync("notes.import", new(first with { Title = "Revised" }), token);
        var query = ArticleFilters.ForSource(first.Source, "read = true");
        var revised = Assert.Single((await api.ListArticlesAsync(new() { Filter = query }, token)).Articles);
        Assert.Equal(article.Name, revised.Name);
        Assert.Equal("Revised", revised.Title);
        Assert.Single(app.Services.GetRequiredService<ArticleStore>().Search("Revised"));
        Assert.Empty(app.Services.GetRequiredService<ArticleStore>().Search("Alpha"));
    }

    [Fact]
    public async Task MissingArticlePluginLeavesSubscriptionsAvailableAndExplainsRefreshFailure()
    {
        using var workspace = new PluginWorkspace();
        using var http = new HttpClient();
        await using (var initial = PluginTestHost.Create(workspace.Database, http))
            initial.Services.GetRequiredService<PluginRuntime>().SetEnabled("articles", false);
        await using var app = PluginTestHost.Create(workspace.Database, http);
        var rss = app.Services.GetRequiredService<IRssApi>();
        var token = TestContext.Current.CancellationToken;
        var feed = await rss.CreateFeedAsync(new() { Feed = new() { Uri = "https://example.com/feed" } }, token);
        var error = await Assert.ThrowsAsync<RpcException>(() =>
            rss.RefreshFeedAsync(new() { Name = feed.Name }, token)
        );
        Assert.Equal(StatusCode.FailedPrecondition, error.StatusCode);
        Assert.Contains("articles.store", error.Status.Detail);
        Assert.Equal(feed, await rss.GetFeedAsync(new() { Name = feed.Name }, token));
    }
}
