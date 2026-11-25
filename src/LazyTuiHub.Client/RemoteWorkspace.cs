using System.Net.Http.Headers;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Client;

public sealed class RemoteWorkspace : IDisposable
{
    private readonly HttpClient http;
    private readonly SessionHandler handler;
    public ClientSession Session { get; }
    public ArticleOperations Articles { get; }
    public FeedOperations Feeds { get; }
    public CapabilityCatalog Catalog { get; }

    public RemoteWorkspace(string endpoint, string? session, string? account, HttpClientHandler? transport = null)
    {
        if (
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var address)
            || !string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
            throw new ArgumentException("Provide an HTTPS endpoint.", nameof(endpoint));
        Session = new ClientSession(session);
        handler = new SessionHandler(Session)
        {
            InnerHandler = transport
                ?? new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true },
        };
        http = new HttpClient(handler, disposeHandler: false) { BaseAddress = address };
        if (!string.IsNullOrEmpty(account))
            http.DefaultRequestHeaders.Add("X-LazyTuiHub-Account", account);
        Articles = new(new ArticleHttpClient(http));
        Feeds = new(new RssHttpClient(http));
        Catalog = new([
            CapabilityGroup.For("access", () => new Sessions(http, Session)),
            CapabilityGroup.For("articles", () => Articles),
            CapabilityGroup.For("rss", () => Feeds),
        ]);
    }

    public void Dispose()
    {
        http.Dispose();
        handler.Dispose();
    }

    private sealed class SessionHandler(ClientSession session) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (session.Token is { } bearer)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class Sessions(HttpClient http, ClientSession session)
    {
        private readonly SessionHttpClient client = new(http);

        [Capability("Sign in to the remote account.")]
        public async Task<IssuedSession> Login(string username, SecretInput password, CancellationToken token)
        {
            var result = await client.AuthenticateUserAsync(
                new() { Username = username, Password = password.Read() },
                token
            );
            session.SignIn(result.Token);
            return new(result.Token, result.Account, result.ExpireTime.ToDateTimeOffset());
        }

        [Capability("Revoke the current session.")]
        public async Task Logout(CancellationToken token)
        {
            await client.RevokeSessionAsync(new(), token);
            session.SignOut();
        }
    }
}
