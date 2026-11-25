namespace LazyTuiHub.Interaction;

public sealed record InteractionOutput(Action<object> Write, Action<string> Notify);
