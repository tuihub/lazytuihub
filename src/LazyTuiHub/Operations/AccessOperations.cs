using LazyTuiHub.Access;
using LazyTuiHub.Framework;
using LazyTuiHub.Interaction;

namespace LazyTuiHub.Operations;

internal sealed class AccessOperations(AccountAuthority authority, ClientSession session)
    : LocalSignIn(authority, session)
{
    private string Session => Identity.Token ?? throw new AccessRequiredException();
    private AccessPrincipal Actor => Authority.Authenticate(Session);

    [Capability("Sign out and revoke the current session.")]
    public void Logout()
    {
        Authority.RevokeSession(Session);
        Identity.SignOut();
    }

    [Capability("Create a user and their account.")]
    public object CreateUser(string username, SecretInput password)
    {
        var (user, account) = Authority.CreateUser(Actor, username, password.Read());
        return new { user, account };
    }

    [Capability("Assign the owner of a device.")]
    public void SetDeviceOwner(string device, string username) => Authority.SetDeviceOwner(Actor, device, username);

    [Capability("Grant read, write or read-write access to a resource URI.")]
    public void Grant(string username, string uri, string mode) =>
        Authority.SetGrant(
            Actor,
            username,
            uri,
            mode switch
            {
                "read" => AccessMode.Read,
                "write" => AccessMode.Write,
                "read-write" => AccessMode.Read | AccessMode.Write,
                _ => throw new ArgumentException("Choose read, write or read-write.", nameof(mode)),
            }
        );

    [Capability("Remove an explicit resource grant.")]
    public void Revoke(string username, string uri) => Authority.SetGrant(Actor, username, uri, AccessMode.None);
}
