using System.Net.Http.Headers;
using LazyTuiHub.Access;
using LazyTuiHub.Client;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Commands;

internal static class AccessCommand
{
    private const string Username = "username";

    public static void Run(WebApplication app, string action)
    {
        var authority = app.Services.GetRequiredService<AccountAuthority>();
        var config = app.Configuration;
        if (string.Equals(action, "login", StringComparison.Ordinal))
        {
            CommandJson.Write(authority.Login(CommandJson.Required(config, Username), Password(config)));
            return;
        }
        var token = CommandJson.Required(config, "Access:Session");
        var actor = authority.Authenticate(token);
        switch (action)
        {
            case "create-user":
                var (user, account) = authority.CreateUser(
                    actor,
                    CommandJson.Required(config, Username),
                    Password(config)
                );
                CommandJson.Write(new { user, account });
                break;
            case "set-device-owner":
                authority.SetDeviceOwner(
                    actor,
                    CommandJson.Required(config, "device"),
                    CommandJson.Required(config, Username)
                );
                break;
            case "grant":
            case "revoke":
                authority.SetGrant(
                    actor,
                    CommandJson.Required(config, Username),
                    CommandJson.Required(config, "uri"),
                    string.Equals(action, "revoke", StringComparison.Ordinal)
                        ? AccessMode.None
                        : Mode(CommandJson.Required(config, "mode"))
                );
                break;
            case "logout":
                authority.RevokeSession(token);
                break;
            default:
                throw new ArgumentException(
                    "Choose login, create-user, set-device-owner, grant, revoke, or logout.",
                    nameof(action)
                );
        }
    }

    public static async Task RunRemoteAsync(IConfiguration configuration, string endpoint, CancellationToken token)
    {
        if (
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var address)
            || !string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
            throw new ArgumentException("Provide an HTTPS endpoint.", nameof(endpoint));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = address };
        var client = new SessionHttpClient(http);
        switch (configuration["action"])
        {
            case "login":
                var result = await client.AuthenticateUserAsync(
                    new()
                    {
                        Username = CommandJson.Required(configuration, Username),
                        Password = Password(configuration),
                    },
                    token
                );
                CommandJson.Write(
                    new
                    {
                        result.Token,
                        result.Account,
                        Expires = result.ExpireTime.ToDateTimeOffset(),
                    }
                );
                break;
            case "logout":
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    CommandJson.Required(configuration, "Access:Session")
                );
                await client.RevokeSessionAsync(new(), token);
                break;
            default:
                throw new ArgumentException("Remote access supports login and logout.", nameof(configuration));
        }
    }

    private static AccessMode Mode(string mode) =>
        mode switch
        {
            "read" => AccessMode.Read,
            "write" => AccessMode.Write,
            "read-write" => AccessMode.Read | AccessMode.Write,
            _ => throw new ArgumentException("Choose read, write, or read-write.", nameof(mode)),
        };

    private static string Password(IConfiguration configuration)
    {
        if (configuration["Access:Password"] is { } supplied)
            return supplied;
        Console.Error.Write("Password: ");
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return password.ToString();
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                password.Length--;
            else if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }
    }
}
