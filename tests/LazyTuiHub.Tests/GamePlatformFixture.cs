using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LazyTuiHub.Tests;

internal sealed class GamePlatformFixture : HttpMessageHandler
{
    internal bool Empty { get; set; }
    internal bool Interrupt { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var uri = request.RequestUri!;
        if (uri.Host == "steamcommunity.com")
        {
            Assert.Contains(
                "openid.mode=check_authentication",
                await request.Content!.ReadAsStringAsync(cancellationToken)
            );
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("ns:http://specs.openid.net/auth/2.0\nis_valid:true\n"),
            };
        }
        if (uri.Host == "api.steampowered.com")
        {
            Assert.Equal("steam-test", Assert.Single(request.Headers.GetValues("x-webapi-key")));
            return Json(
                Empty ? """{"response":{"game_count":0}}"""
                : Interrupt ? """{"response":{}}"""
                : """{"response":{"game_count":1,"games":[{"appid":324160,"name":"CLANNAD"}]}}"""
            );
        }
        if (uri.Host == "api.bgm.tv")
        {
            Assert.Equal("Bearer bgm-test", request.Headers.Authorization!.ToString());
            if (uri.AbsolutePath == "/v0/me")
                return Json("""{"id":42,"username":"alice","nickname":"Alice"}""");
            Assert.Equal("/v0/users/alice/collections", uri.AbsolutePath);
            if (Empty)
                return Json("""{"total":0,"data":[]}""");
            if (Interrupt && uri.Query.Contains("offset=1", StringComparison.Ordinal))
                return new(HttpStatusCode.BadGateway);
            var count = Interrupt ? 2 : 1;
            return Json(
                $$$"""{"total":{{{count}}},"data":[{"subject_id":13,"type":3,"subject":{"name":"CLANNAD"}}]}"""
            );
        }
        Assert.Equal("Token vn-test", request.Headers.Authorization!.ToString());
        if (uri.AbsolutePath.EndsWith("authinfo", StringComparison.Ordinal))
            return Json("""{"id":"u123","username":"Alice","permissions":["listread"]}""");
        var query = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("u123", query.GetProperty("user").GetString());
        return Json(
            """{"more":false,"results":[{"id":"v4","vn":{"title":"CLANNAD"},"labels":[{"label":"Finished"}]}]}"""
        );
    }

    private static HttpResponseMessage Json(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, System.Text.Encoding.UTF8, "application/json") };
}
