namespace LazyTuiHub.Framework;

public sealed record AccessPrincipal(
    string User,
    string Account,
    Func<IReadOnlyList<UriGrant>> Grants,
    bool OwnsAccount = false
)
{
    public bool Allows(string uri, AccessMode mode) => (Permissions(uri, Grants()) & mode) == mode;

    internal AccessMode Permissions(string uri, IReadOnlyList<UriGrant> grants)
    {
        var effective = AccessMode.None;
        foreach (var grant in grants.Where(grant => ResourceUri.Covers(grant.Uri, uri)))
            effective |= grant.Mode;
        if (
            OwnsAccount
            && ResourceUri.Segments(uri) is [not "core", "accounts", var account, ..]
            && string.Equals(account, Account, StringComparison.Ordinal)
        )
            effective |= AccessMode.Read | AccessMode.Write;
        return effective;
    }
}
