using System.Net.Http.Headers;
using System.Text.Json;
using LazyTuiHub.Client;
using Microsoft.Extensions.Configuration;

namespace LazyTuiHub.Cli;

public static class RemoteConsole
{
    public const string Help = """
        LazyTuiHub CLI
        --endpoint HTTPS_URL --command GROUP --action ACTION [options]
          access     login --username USER | logout
          articles   list [--source NAME] [--unread true] [--page-token TOKEN] | get/read --name articles/ID
          rss        feeds [--page-token TOKEN] | add --feed-url URI [--title NAME] | get/refresh --name feeds/ID
        Use Access__Session for authenticated requests and Access__Account to select a shared account.
        Use the application host with --cli for local plugins and workspace commands.
        """;

    public static async Task<int> RunAsync(IConfiguration options, CancellationToken token)
    {
        try
        {
            var group = options["command"];
            if (group is null || options["action"] is null or "help")
            {
                Console.WriteLine(Help);
                return 0;
            }
            if (
                !Uri.TryCreate(options["endpoint"], UriKind.Absolute, out var address)
                || !string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            )
                throw new ArgumentException("Provide an HTTPS endpoint.", nameof(options));
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = true,
            };
            using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = address };
            if (options["Access:Session"] is { Length: > 0 } session)
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session);
            if (options["Access:Account"] is { Length: > 0 } account)
                http.DefaultRequestHeaders.Add("X-LazyTuiHub-Account", account);
            var action = options["action"]!;
            switch (group)
            {
                case "access":
                    await SessionAsync(http, options, token);
                    break;
                case "articles":
                    await ReaderCommands.ArticlesAsync(new ArticleHttpClient(http), options, action, token);
                    break;
                case "rss":
                    await ReaderCommands.FeedsAsync(new RssHttpClient(http), options, action, token);
                    break;
                default:
                    throw new ArgumentException($"Unknown remote command '{group}'.", nameof(options));
            }
            return 0;
        }
        catch (Exception error)
            when (error
                    is SessionApiException
                        or ArticleApiException
                        or RssApiException
                        or HttpRequestException
                        or ArgumentException
                        or FormatException
                        or IOException
            )
        {
            await Console.Error.WriteLineAsync(error.Message.AsMemory(), token);
            return 1;
        }
    }

    private static async Task SessionAsync(HttpClient http, IConfiguration configuration, CancellationToken token)
    {
        var client = new SessionHttpClient(http);
        switch (configuration["action"])
        {
            case "login":
                var result = await client.AuthenticateUserAsync(
                    new()
                    {
                        Username = ReaderCommands.Required(configuration, "username"),
                        Password = ReadPassword(configuration),
                    },
                    token
                );
                Console.WriteLine(
                    JsonSerializer.Serialize(
                        new LoginOutput(result.Token, result.Account, result.ExpireTime.ToDateTimeOffset()),
                        CliJsonContext.Default.LoginOutput
                    )
                );
                break;
            case "logout":
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    ReaderCommands.Required(configuration, "Access:Session")
                );
                await client.RevokeSessionAsync(new(), token);
                break;
            default:
                throw new ArgumentException("Remote access supports login and logout.", nameof(configuration));
        }
    }

    public static string ReadPassword(IConfiguration configuration)
    {
        if (configuration["Access:Password"] is { } supplied)
            return supplied;
        if (Console.IsInputRedirected)
            return Console.ReadLine()
                ?? throw new EndOfStreamException("Supply a password on stdin or via Access__Password.");
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
