using System.Security.Cryptography;
using System.Text.Json;
using Grpc.Core;
using Microsoft.AspNetCore.DataProtection;

namespace LazyTuiHub.Rss.Application;

internal sealed class PageTokens(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("LazyTuiHub.Pagination");

    public string Encode(string scope, string cursor) =>
        protector.Protect(JsonSerializer.Serialize(new Cursor(scope, cursor)));

    public string Decode(string scope, string token, string defaultCursor)
    {
        if (string.IsNullOrEmpty(token))
            return defaultCursor;
        try
        {
            var cursor = JsonSerializer.Deserialize<Cursor>(protector.Unprotect(token));
            if (cursor is null || !cursor.Scope.Equals(scope, StringComparison.Ordinal))
                throw new FormatException();
            return cursor.Value;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            throw ApiErrors.Create(
                StatusCode.InvalidArgument,
                "INVALID_PAGE_TOKEN",
                "The page token is invalid for this query."
            );
        }
    }

    private sealed record Cursor(string Scope, string Value);
}
