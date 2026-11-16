namespace LazyTuiHub.Framework;

public abstract record DataPacket(DataReference? Reference)
{
    public abstract object Payload { get; }
}

public sealed record DataPacket<T>(T Value, DataReference? Reference = null) : DataPacket(Reference)
    where T : notnull
{
    public override object Payload => Value;
}
