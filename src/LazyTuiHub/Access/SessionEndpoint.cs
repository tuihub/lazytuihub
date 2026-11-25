using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LazyTuiHub.Api;
using LazyTuiHub.Api.Access.V1;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Access;

internal sealed class SessionEndpoint(AccountAuthority authority) : SessionService.SessionServiceBase
{
    public override Task<AuthenticateUserResponse> AuthenticateUser(
        AuthenticateUserRequest request,
        ServerCallContext context
    )
    {
        try
        {
            var result = authority.Login(request.Username, request.Password);
            return Task.FromResult(
                new AuthenticateUserResponse
                {
                    Token = result.Token,
                    Account = result.Account,
                    ExpireTime = Timestamp.FromDateTimeOffset(result.Expires),
                }
            );
        }
        catch (AccessRequiredException)
        {
            throw RpcErrors.Create(StatusCode.Unauthenticated, "INVALID_CREDENTIALS", "The credentials are invalid.");
        }
    }

    public override Task<RevokeSessionResponse> RevokeSession(RevokeSessionRequest request, ServerCallContext context)
    {
        authority.RevokeSession(AccessHosting.Token(context.GetHttpContext().Request)!);
        return Task.FromResult(new RevokeSessionResponse());
    }
}
