namespace LazyTuiHub.Framework;

public sealed record PortDescriptor(string Name, Type DataType, PortDelivery Delivery)
{
    public static PortDescriptor Stream<T>(string name) => new(name, typeof(T), PortDelivery.Streaming);

    public static PortDescriptor Sync<T>(string name) => new(name, typeof(T), PortDelivery.Synchronized);
}
