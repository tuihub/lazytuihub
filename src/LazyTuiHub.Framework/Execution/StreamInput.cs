namespace LazyTuiHub.Framework;

public sealed record StreamInput(string Port, DataPacket Packet) : NodeInput(PortDelivery.Streaming)
{
    public T Get<T>()
        where T : notnull => ((DataPacket<T>)Packet).Value;
}
