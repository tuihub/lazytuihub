using System.Globalization;
using System.Net;
using LazyTuiHub.Plugin.TuiHub.Games;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Plugin.TuiHub.Steam;

internal static class SteamSignIn
{
    private const string Endpoint = "https://steamcommunity.com/openid/login";
    private const string Namespace = "http://specs.openid.net/auth/2.0";
    private const string SelectId = Namespace + "/identifier_select";
    private const string Identity = "openid.identity";
    private const string Claimed = "openid.claimed_id";
    private const string ReturnTo = "openid.return_to";
    private const string Mode = "openid.mode";
    private const string SteamIds = "https://steamcommunity.com/openid/id/";

    public static async Task<string> AuthenticateAsync(
        HttpClient client,
        Action<Uri> showBrowser,
        CancellationToken token
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var callback = builder.Build();
        var path = "/steam/" + Guid.NewGuid().ToString("N");
        var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        string returnTo = string.Empty;
        callback.MapGet(path, async (HttpContext context) =>
        {
            var fields = context.Request.Query;
            if (fields[Mode] == "cancel")
            {
                completed.TrySetCanceled(token);
                return Results.Text("Steam sign-in cancelled.");
            }
            var claimed = fields[Claimed].ToString();
            var signed = fields["openid.signed"].ToString().Split(',');
            string[] required =
            [
                "op_endpoint",
                "claimed_id",
                "identity",
                "return_to",
                "response_nonce",
                "assoc_handle",
            ];
            if (
                fields[Mode] != "id_res"
                || fields["openid.ns"] != Namespace
                || fields["openid.op_endpoint"] != Endpoint
                || fields[ReturnTo] != returnTo
                || fields[Identity] != claimed
                || !required.All(signed.Contains)
                || !claimed.StartsWith(SteamIds, StringComparison.Ordinal)
                || !ulong.TryParse(
                    claimed.AsSpan(SteamIds.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var id
                )
            )
                return Results.BadRequest();
            var proof = fields
                .Where(pair => pair.Key.StartsWith("openid.", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value.Single()!, StringComparer.Ordinal);
            proof[Mode] = "check_authentication";
            using var form = new FormUrlEncodedContent(proof);
            string body;
            try
            {
                using var verified = await client.PostAsync(new Uri(Endpoint), form, token);
                verified.EnsureSuccessStatusCode();
                body = await verified.Content.ReadAsStringAsync(token);
            }
            catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException)
            {
                completed.TrySetException(failure);
                return Results.Text("Steam could not verify the sign-in. Return to LazyTuiHub.");
            }
            if (!body.Split('\n', StringSplitOptions.TrimEntries).Contains("is_valid:true", StringComparer.Ordinal))
            {
                completed.TrySetException(
                    new InvalidOperationException("Steam rejected the sign-in. Connect the account again.")
                );
                return Results.BadRequest("Steam rejected the sign-in. Return to LazyTuiHub.");
            }
            context.Response.OnCompleted(() =>
            {
                completed.TrySetResult(id.ToString(CultureInfo.InvariantCulture));
                return Task.CompletedTask;
            });
            return Results.Text("Steam account verified. Return to LazyTuiHub.");
        });
        await callback.StartAsync(token);
        var realm = callback.Urls.Single();
        returnTo = realm + path;
        var url = QueryHelpers.AddQueryString(
            Endpoint,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["openid.ns"] = Namespace,
                [Mode] = "checkid_setup",
                [ReturnTo] = returnTo,
                ["openid.realm"] = realm + "/",
                [Identity] = SelectId,
                [Claimed] = SelectId,
            }
        );
        try
        {
            showBrowser(new Uri(url));
            return await completed.Task.WaitAsync(token);
        }
        finally
        {
            await callback.StopAsync(CancellationToken.None);
        }
    }
}
