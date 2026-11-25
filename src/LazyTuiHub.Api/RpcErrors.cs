using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;

namespace LazyTuiHub.Api;

public static class RpcErrors
{
    public static RpcException Create(StatusCode code, string reason, string message)
    {
        var status = new Google.Rpc.Status { Code = (int)code, Message = message };
        status.Details.Add(Any.Pack(new ErrorInfo { Reason = reason, Domain = "lazytuihub.tuihub.org" }));
        return new(
            new Grpc.Core.Status(code, message),
            new Metadata { { "grpc-status-details-bin", status.ToByteArray() } }
        );
    }
}
