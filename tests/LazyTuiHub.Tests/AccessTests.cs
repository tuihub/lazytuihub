using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LazyTuiHub.Api.Access.V1;
using LazyTuiHub.Api.Rss.V1;
using LazyTuiHub.Client;
using LazyTuiHub.Examples;
using LazyTuiHub.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LazyTuiHub.Tests;

public sealed class AccessTests
{
    private const string Admin = "admin";
    private const string Reader = "reader";
    private const string AccountHeader = "X-LazyTuiHub-Account";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScopedAccessControlsDataAndRemoteDevicePlacement()
    {
        using var workspace = new PluginWorkspace();
        using var certificate = LocalCertificate();
        var certificateFile = Path.Combine(workspace.DirectoryPath, "loopback.pfx");
        var password = Guid.NewGuid().ToString("N");
        var peerSecret = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(certificateFile, certificate.Export(X509ContentType.Pkcs12, password), Token);
        await using var fixture = Fixture();
        await fixture.StartAsync(Token);
        await using var worker = ApplicationHost.Create([
            "--worker=true",
            "--Access:Enabled=true",
            "--urls=http://127.0.0.1:0",
            $"--database={Path.Combine(workspace.DirectoryPath, "worker.db")}",
            "--Cluster:Id=fetcher",
            $"--Cluster:Token={peerSecret}",
            "--Cluster:AllowInsecureHttp=true",
            "--Cluster:Export:0=builtin.http-fetch",
            $"--Cluster:FetchOrigins:0={new Uri(fixture.Urls.Single()).GetLeftPart(UriPartial.Authority)}",
        ]);
        await worker.StartAsync(Token);
        await using var primary = ApplicationHost.Create([
            "--Access:Enabled=true",
            $"--Access:AdminUser={Admin}",
            $"--Access:AdminPassword={password}",
            $"--database={workspace.Database}",
            "--Kestrel:Endpoints:Main:Url=https://127.0.0.1:0",
            $"--Kestrel:Endpoints:Main:Certificate:Path={certificateFile}",
            $"--Kestrel:Endpoints:Main:Certificate:Password={password}",
            "--Cluster:Id=primary",
            "--Cluster:Resources:0=workspace.primary",
            "--Cluster:AllowInsecureHttp=true",
            $"--Cluster:Peers:0:Url={worker.Urls.Single()}",
            $"--Cluster:Peers:0:Token={peerSecret}",
        ]);
        await primary.StartAsync(Token);
        var endpoint = new Uri(primary.Urls.Single());
        using var admin = Client(endpoint, certificate);
        using var reader = Client(endpoint, certificate);
        var adminSession = await AuthenticateUserAsync(admin, Admin, password);
        await AdminAsync(
            workspace,
            adminSession.Token,
            "create-user",
            $"--username={Reader}",
            $"--Access:Password={password}"
        );
        var readerSession = await AuthenticateUserAsync(reader, Reader, password);
        Assert.NotEqual(adminSession.Account, readerSession.Account);

        var feed = new CreateFeedRequest
        {
            Feed = new() { Uri = new Uri(new Uri(fixture.Urls.Single()), "/feed").AbsoluteUri },
            FeedId = Guid.NewGuid().ToString("D"),
            RequestId = Guid.NewGuid().ToString("D"),
        };
        var adminRss = new RssHttpClient(admin);
        var readerRss = new RssHttpClient(reader);
        var privateFeed = await adminRss.CreateFeedAsync(feed.Feed, feed.FeedId, feed.RequestId, Token);
        Assert.Empty((await readerRss.ListFeedsAsync(null, null, Token)).Feeds);
        Assert.Equal(
            privateFeed.Name,
            (await readerRss.CreateFeedAsync(feed.Feed, feed.FeedId, feed.RequestId, Token)).Name
        );

        await VerifySharedFeedAsync(workspace, reader, readerRss, adminSession, feed, privateFeed.Name);
        await VerifyDeviceAsync(workspace, readerRss, adminSession.Token, readerSession.Token, privateFeed.Name);
        await VerifyArticlesAsync(admin, reader);
        await new SessionHttpClient(reader).RevokeSessionAsync(new(), Token);
        await RssStatusAsync(HttpStatusCode.Unauthorized, () => readerRss.ListFeedsAsync(null, null, Token));
    }

    private static async Task VerifySharedFeedAsync(
        PluginWorkspace workspace,
        HttpClient reader,
        RssHttpClient rss,
        AuthenticateUserResponse owner,
        CreateFeedRequest feed,
        string name
    )
    {
        reader.DefaultRequestHeaders.Add(AccountHeader, owner.Account);
        Assert.Empty((await rss.ListFeedsAsync(null, null, Token)).Feeds);
        await RssStatusAsync(HttpStatusCode.NotFound, () => rss.GetFeedAsync(feed.FeedId, Token));
        var uri = ResourceUri.InAccount("rss", owner.Account, "feeds", feed.FeedId);
        await AdminAsync(workspace, owner.Token, "grant", $"--username={Reader}", $"--uri={uri}", "--mode=read");
        Assert.Equal(name, (await rss.GetFeedAsync(feed.FeedId, Token)).Name);
        Assert.Single((await rss.ListFeedsAsync(1, null, Token)).Feeds);
        await RssStatusAsync(HttpStatusCode.Forbidden, () =>
            rss.RefreshFeedAsync(feed.FeedId, new() { Name = name }, Token)
        );
        await AdminAsync(workspace, owner.Token, "revoke", $"--username={Reader}", $"--uri={uri}");
        await RssStatusAsync(HttpStatusCode.NotFound, () => rss.GetFeedAsync(feed.FeedId, Token));
        reader.DefaultRequestHeaders.Remove(AccountHeader);
    }

    private static async Task VerifyDeviceAsync(
        PluginWorkspace workspace,
        RssHttpClient rss,
        string ownerToken,
        string readerToken,
        string name
    )
    {
        var feedId = name.Split('/')[1];
        Task RefreshAsync() => rss.RefreshFeedAsync(feedId, new() { Name = name }, Token);
        await AdminAsync(workspace, ownerToken, "set-device-owner", "--device=fetcher", $"--username={Admin}");
        await RssStatusAsync(HttpStatusCode.Forbidden, RefreshAsync);
        var jobs = $"--uri={ResourceUri.DeviceJobs("fetcher")}";
        await AdminAsync(workspace, ownerToken, "grant", $"--username={Reader}", jobs, "--mode=write");
        await RefreshAsync();
        var unauthorized = await workspace.RunAsync(
            "access",
            "grant",
            "--Access:Enabled=true",
            $"--Access:Session={readerToken}",
            $"--username={Admin}",
            jobs,
            "--mode=read-write"
        );
        Assert.NotEqual(0, unauthorized.Code);
        await AdminAsync(workspace, ownerToken, "revoke", $"--username={Reader}", jobs);
        await RssStatusAsync(HttpStatusCode.Forbidden, RefreshAsync);
    }

    private static async Task VerifyArticlesAsync(HttpClient admin, HttpClient reader)
    {
        var readerApi = new ArticleHttpClient(reader);
        var adminApi = new ArticleHttpClient(admin);
        var page = await readerApi.ListArticlesAsync(1, null, null, Token);
        var id = Assert.Single(page.Articles).Name.Split('/')[1];
        Assert.NotEmpty(page.NextPageToken);
        Assert.Empty((await adminApi.ListArticlesAsync(null, null, null, Token)).Articles);
        var hidden = await Assert.ThrowsAsync<ArticleApiException>(() => adminApi.GetArticleAsync(id, Token));
        Assert.Equal((int)HttpStatusCode.NotFound, hidden.StatusCode);
        var invalid = await Assert.ThrowsAsync<ArticleApiException>(() =>
            adminApi.ListArticlesAsync(1, page.NextPageToken, null, Token)
        );
        Assert.Equal((int)HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static Task<string> AdminAsync(
        PluginWorkspace workspace,
        string session,
        string action,
        params string[] options
    ) => workspace.SuccessAsync("access", action, ["--Access:Enabled=true", $"--Access:Session={session}", .. options]);

    private static async Task RssStatusAsync(HttpStatusCode status, Func<Task> call) =>
        Assert.Equal((int)status, (await Assert.ThrowsAsync<RssApiException>(call)).StatusCode);

    private static async Task<AuthenticateUserResponse> AuthenticateUserAsync(
        HttpClient http,
        string username,
        string password
    )
    {
        var session = await new SessionHttpClient(http).AuthenticateUserAsync(
            new() { Username = username, Password = password },
            Token
        );
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return session;
    }

    private static WebApplication Fixture()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/feed", () => Results.Text(RssDemo.FeedXml, "application/rss+xml"));
        return app;
    }

    private static X509Certificate2 LocalCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        var now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(1));
    }

    private static HttpClient Client(Uri endpoint, X509Certificate2 certificate) =>
        new(
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, remote, _, _) => certificate.Equals(remote),
            }
        )
        {
            BaseAddress = endpoint,
        };
}
