using System.Net.Http.Headers;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Access;

internal static class AccessHosting
{
    internal const string AccountHeader = "X-LazyTuiHub-Account";

    public static void Map(WebApplication app)
    {
        var authority = app.Services.GetRequiredService<AccountAuthority>();
        var access = app.Services.GetRequiredService<ResourceAccess>();
        app.Use(
            async (context, next) =>
            {
                if (!context.Request.IsHttps)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                context.Response.Headers.CacheControl = "no-store";
                var path = context.Request.Path.Value;
                if (path is "/v1/sessions:authenticateUser" or "/lazytuihub.access.v1.SessionService/AuthenticateUser")
                {
                    await next(context);
                    return;
                }

                var token = Token(context.Request);
                AccessPrincipal principal;
                try
                {
                    principal = authority.Authenticate(
                        token ?? throw new AccessRequiredException(),
                        context.Request.Headers[AccountHeader].FirstOrDefault()
                    );
                }
                catch (AccessRequiredException)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                catch (AccessDeniedException)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                using (access.Enter(principal))
                    await next(context);
            }
        );
        app.MapGrpcService<SessionEndpoint>();
    }

    internal static string? Token(HttpRequest request) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header)
        && header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            ? header.Parameter
            : null;
}
