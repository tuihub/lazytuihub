namespace LazyTuiHub.Framework;

public abstract record DataPacket(DataReference? Reference)
{
    public abstract object Payload { get; }
}

public sealed record DataPacket<T>(
    [property: System.Text.Json.Serialization.JsonRequired] T Value,
    DataReference? Reference = null
) : DataPacket(Reference)
    where T : notnull
{
    [System.Text.Json.Serialization.JsonIgnore]
    public override object Payload => Value;
}
