using System.Net;
using LazyTuiHub.Articles;
using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using LazyTuiHub.Nodes;
using LazyTuiHub.Plugins;
using LazyTuiHub.Rss;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class RssTests
{
    [Fact]
    public async Task RefreshPersistsIdentityRelationsAndReadStateAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lazytuihub-rss-test-{Guid.NewGuid():N}");
        using var handler = new FeedHandler();
        using var http = new HttpClient(handler);
        try
        {
            var path = Path.Combine(directory, "workspace.db");
            await using var app = PluginTestHost.Create(path, http);
            var workspace = app.Services.GetRequiredService<RssOperations>();
            var workflows = app.Services.GetRequiredService<WorkflowRuntime>();
            var request = new SubscribeFeed(
                string.Empty,
                new("https://example.com/feed.xml"),
                "Example",
                Guid.NewGuid().ToString("D")
            );
            var feed = Assert.Single(
                await workflows.CollectAsync<FeedRecord>(
                    "rss.subscribe",
                    request,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
            Assert.Equal(
                2,
                await workspace.RefreshAsync(new(feed.Id), cancellationToken: TestContext.Current.CancellationToken)
            );
            var articles = await workflows.CollectAsync<ArticleRecord>(
                "articles.list",
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Null(Assert.Single(app.Services.GetRequiredService<RssDetails>().Read()).ExpiresAt);
            Assert.Single(app.Services.GetRequiredService<ArticleStore>().Search("streaming"));
            var article = articles[0];
            await workflows.CollectAsync<ArticleRecord>(
                "articles.mark-read",
                article.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
            workflows.Save(workflows.Get("rss.ingest") with { BufferCapacity = 1 });

            await using var restartedApp = PluginTestHost.Create(path, http);
            var restarted = restartedApp.Services.GetRequiredService<RssOperations>();
            var restartedWorkflows = restartedApp.Services.GetRequiredService<WorkflowRuntime>();
            Assert.Equal(
                feed,
                Assert.Single(
                    await restartedWorkflows.CollectAsync<FeedRecord>(
                        "rss.subscribe",
                        request,
                        cancellationToken: TestContext.Current.CancellationToken
                    )
                )
            );
            handler.Xml = RssDemo.FeedXml.Replace("Hello streaming", "Updated title", StringComparison.Ordinal);
            Assert.Equal(
                2,
                await restarted.RefreshAsync(new(feed.Id), cancellationToken: TestContext.Current.CancellationToken)
            );
            var refreshed = await restartedWorkflows.CollectAsync<ArticleRecord>(
                "articles.list",
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Equal(2, refreshed.Count);
            Assert.Empty(restartedApp.Services.GetRequiredService<ArticleStore>().Search("streaming"));
            Assert.Single(restartedApp.Services.GetRequiredService<ArticleStore>().Search("Updated"));
            Assert.Equal(2, restartedApp.Services.GetRequiredService<RssDetails>().Read().Count);
            Assert.True(refreshed.Single(value => value.Id == article.Id).IsRead);
            Assert.Contains(refreshed, value => value.Article.Title == "Updated title");
            Assert.Equal(2, restartedWorkflows.Catalog.ReadRelations(feed.Reference).Count);
            Assert.Equal(1, restartedWorkflows.Get("rss.ingest").BufferCapacity);
            var conflict = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
                restartedWorkflows.CollectAsync<FeedRecord>(
                    "rss.subscribe",
                    request with
                    {
                        Url = new("https://example.org/other"),
                    },
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
            Assert.IsType<RssConflictException>(conflict.InnerException);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LocalAndGeneratedHttpClientsExerciseTheSamePipelines() => await RssDemo.RunAsync();

    private sealed class FeedHandler : HttpMessageHandler
    {
        public string Xml { get; set; } = RssDemo.FeedXml;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Xml) });
    }
}
