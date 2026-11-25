namespace LazyTuiHub.Sdk.Avalonia;

public delegate Task RunDesktopOperation(Func<CancellationToken, Task> operation);
