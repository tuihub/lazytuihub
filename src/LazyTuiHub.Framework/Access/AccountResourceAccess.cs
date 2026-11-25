namespace LazyTuiHub.Framework;

public sealed record AccountResourceAccess(string Plugin, string Collection, AccessMode Mode)
{
    public string UriFor(string account) => ResourceUri.InAccount(Plugin, account, Collection);
}
