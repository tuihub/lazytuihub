namespace LazyTuiHub.Tests;

public sealed class ProbeState
{
    public int Cleanups { get; set; }
    public int Uninstalls { get; set; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
