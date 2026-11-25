using System.Text.Json;

namespace LazyTuiHub.Framework;

public interface IExecutionPeer
{
    string Endpoint { get; }
    Task<WorkerInventory> InspectAsync(CancellationToken token);
    Task<byte[]> InvokeAsync(
        string instance,
        string operation,
        IReadOnlyList<string> requirements,
        PacketExecutionMode mode,
        JsonElement settings,
        byte[] input,
        CancellationToken token
    );
}
