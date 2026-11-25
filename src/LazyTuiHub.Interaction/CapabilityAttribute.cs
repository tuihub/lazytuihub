namespace LazyTuiHub.Interaction;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CapabilityAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}
