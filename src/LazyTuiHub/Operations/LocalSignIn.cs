using LazyTuiHub.Access;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Operations;

internal class LocalSignIn(AccountAuthority authority, ClientSession session)
{
    protected AccountAuthority Authority { get; } = authority;
    protected ClientSession Identity { get; } = session;

    [Capability("Sign in with an account name and password.")]
    public IssuedSession Login(string username, SecretInput password)
    {
        var credential = Authority.Login(username, password.Read());
        Identity.SignIn(credential.Token);
        return credential;
    }
}
