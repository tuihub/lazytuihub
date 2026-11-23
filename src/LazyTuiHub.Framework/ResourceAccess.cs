namespace LazyTuiHub.Framework;

public sealed class ResourceAccess(bool singleAccount, AccessPrincipal? localPrincipal = null)
{
    public const string LocalAccount = "local";
    private readonly AsyncLocal<AccessPrincipal?> active = new();

    public string Account => singleAccount ? LocalAccount : Principal.Account;
    public bool IsSingleAccount => singleAccount;
    private AccessPrincipal Principal => active.Value ?? localPrincipal ?? throw new AccessRequiredException();

    public IDisposable Enter(AccessPrincipal principal)
    {
        var previous = active.Value;
        active.Value = principal;
        return new Restore(this, previous);
    }

    public bool Allows(string uri, AccessMode mode) => singleAccount || Principal.Allows(uri, mode);

    public void Require(string uri, AccessMode mode)
    {
        if (singleAccount)
            return;
        var principal = Principal;
        var permissions = principal.Permissions(uri, principal.Grants());
        if ((permissions & mode) == mode)
            return;
        if (!permissions.HasFlag(AccessMode.Read))
            throw new KeyNotFoundException("The requested resource does not exist.");
        throw new AccessDeniedException();
    }

    public IReadOnlyDictionary<string, AccessMode> Check(IEnumerable<string> uris)
    {
        if (singleAccount)
            return uris.Distinct(StringComparer.Ordinal)
                .ToDictionary(uri => uri, _ => AccessMode.Read | AccessMode.Write, StringComparer.Ordinal);

        var principal = Principal;
        var grants = principal.Grants();
        return uris.Distinct(StringComparer.Ordinal)
            .ToDictionary(uri => uri, uri => principal.Permissions(uri, grants), StringComparer.Ordinal);
    }

    public IReadOnlyList<UriGrant> Within(string scope)
    {
        if (singleAccount)
            return [new(scope, AccessMode.Read | AccessMode.Write)];

        var principal = Principal;
        var grants = principal.Grants();
        var effective = principal.Permissions(scope, grants);
        var children = grants.Where(grant =>
            ResourceUri.Covers(scope, grant.Uri)
            && !string.Equals(scope, grant.Uri, StringComparison.Ordinal)
            && (grant.Mode & ~effective) != AccessMode.None
        );
        return effective == AccessMode.None ? children.ToArray() : [new(scope, effective), .. children];
    }

    // Null represents access to the whole collection. A non-null result contains the
    // direct child identifiers covered by the effective grants.
    public string[]? ReadableChildren(string collection)
    {
        var grants = Within(collection);
        if (
            grants.Any(grant =>
                grant.Mode.HasFlag(AccessMode.Read) && string.Equals(collection, grant.Uri, StringComparison.Ordinal)
            )
        )
            return null;
        var prefixLength = ResourceUri.Segments(collection).Length;
        return grants
            .Where(grant => grant.Mode.HasFlag(AccessMode.Read))
            .Select(grant => ResourceUri.Segments(grant.Uri))
            .Where(parts => parts.Length == prefixLength + 1)
            .Select(parts => parts[^1])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class Restore(ResourceAccess owner, AccessPrincipal? previous) : IDisposable
    {
        public void Dispose() => owner.active.Value = previous;
    }
}
