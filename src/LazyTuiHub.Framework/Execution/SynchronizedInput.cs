namespace LazyTuiHub.Framework;

public sealed record SynchronizedInput(string MessageKey, IReadOnlyDictionary<string, DataPacket> Values)
    : NodeInput(PortDelivery.Synchronized)
{
    public T Get<T>(string port)
        where T : notnull => ((DataPacket<T>)Values[port]).Value;
}
