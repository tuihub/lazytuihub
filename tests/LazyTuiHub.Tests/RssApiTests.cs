using System.Net;
using System.Text.Json;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Examples;
using LazyTuiHub.Plugin.TuiHub.Articles.Api;
using LazyTuiHub.Plugin.TuiHub.Articles.Frontend;
using LazyTuiHub.Plugin.TuiHub.Rss.Api;
using LazyTuiHub.Plugin.TuiHub.Rss.Frontend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class RssApiTests
{
    [Fact]
    public async Task PaginationErrorsAndRequestIdsSurviveHttpAndRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lazytuihub-api-test-{Guid.NewGuid():N}");
        var database = Path.Combine(directory, "workspace.db");
        var request = new CreateFeedRequest { RequestId = Guid.NewGuid().ToString("D") };
        Feed saved;
        string pageToken;
        try
        {
            await using (var app = ApplicationHost.Create(["--demo", "true", "--database", database]))
            {
                app.MapGet("/fixture", () => Results.Text(RssFixture.FeedXml, "application/rss+xml"));
                await app.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
                try
                {
                    using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
                    IRssApi remote = new RssHttpClient(http);
                    var articleApi = new ArticleHttpClient(http);
                    request.Feed = new()
                    {
                        Uri = new Uri(http.BaseAddress, "fixture").AbsoluteUri,
                        DisplayName = "API fixture",
                    };
                    saved = await remote.CreateFeedAsync(
                        request,
                        cancellationToken: TestContext.Current.CancellationToken
                    );
                    Assert.Equal(
                        saved,
                        await remote.GetFeedAsync(
                            new() { Name = saved.Name },
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                    );
                    await remote.RefreshFeedAsync(
                        new() { Name = saved.Name },
                        cancellationToken: TestContext.Current.CancellationToken
                    );
                    var first = await articleApi.ListArticlesAsync(
                        new() { Filter = ArticleFilters.ForSource(saved.Name), PageSize = 1 },
                        cancellationToken: TestContext.Current.CancellationToken
                    );
                    Assert.Single(first.Articles);
                    Assert.NotEmpty(first.NextPageToken);
                    pageToken = first.NextPageToken;
                    var second = await articleApi.ListArticlesAsync(
                        new()
                        {
                            Filter = ArticleFilters.ForSource(saved.Name),
                            PageSize = 5,
                            PageToken = pageToken,
                        },
                        cancellationToken: TestContext.Current.CancellationToken
                    );
                    Assert.Single(second.Articles);
                    Assert.NotEqual(first.Articles[0].Name, second.Articles[0].Name);
                    Assert.Empty(second.NextPageToken);

                    var error = await Assert.ThrowsAsync<ArticleApiException>(() =>
                        articleApi.ListArticlesAsync(
                            new()
                            {
                                Filter = ArticleFilters.ForSource(saved.Name, "read = false"),
                                PageToken = pageToken,
                            },
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                    );
                    Assert.Equal((int)HttpStatusCode.BadRequest, error.StatusCode);
                    using var details = JsonDocument.Parse(Assert.IsType<string>(error.Response));
                    Assert.Contains(details.RootElement.GetProperty("details").EnumerateArray(), value =>
                        value.GetProperty("reason").GetString() == "INVALID_PAGE_TOKEN"
                    );

                    var invalid = await Assert.ThrowsAsync<RssApiException>(() =>
                        remote.CreateFeedAsync(
                            new() { Feed = request.Feed, RequestId = Guid.Empty.ToString("D") },
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                    );
                    Assert.Equal((int)HttpStatusCode.BadRequest, invalid.StatusCode);
                    var duplicate = await Assert.ThrowsAsync<RssApiException>(() =>
                        remote.CreateFeedAsync(
                            new() { Feed = request.Feed },
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                    );
                    Assert.Equal((int)HttpStatusCode.Conflict, duplicate.StatusCode);
                }
                finally
                {
                    await app.StopAsync(cancellationToken: CancellationToken.None);
                }
            }
            await using var restarted = ApplicationHost.Create(["--demo", "true", "--database", database]);
            var local = restarted.Services.GetRequiredService<IRssApi>();
            Assert.Equal(
                saved,
                await local.CreateFeedAsync(request, cancellationToken: TestContext.Current.CancellationToken)
            );
            Assert.Single(
                (
                    await restarted
                        .Services.GetRequiredService<IArticleApi>()
                        .ListArticlesAsync(
                            new() { Filter = ArticleFilters.ForSource(saved.Name), PageToken = pageToken },
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                ).Articles
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
