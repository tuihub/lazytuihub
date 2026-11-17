using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace LazyTuiHub.Desktop;

public abstract partial class PageModel(DesktopSession session, ILogger logger) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Busy { get; private set; }
    public bool Failed { get; private set; }
    public bool Idle => !Busy;
    public string Status { get; protected set; } = string.Empty;

    protected void Changed() => PropertyChanged?.Invoke(this, new(string.Empty));

    protected Task RunAsync(Func<CancellationToken, Task> operation) =>
        session.RunAsync(async token =>
        {
            if (Busy)
                return;
            Busy = true;
            Failed = false;
            Status = "Working…";
            Changed();
            try
            {
                await operation(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Status = "Stopped.";
            }
            catch (Exception error)
            {
                Failed = true;
                Status = error.GetBaseException().Message;
                LogOperationFailure(logger, error);
            }
            finally
            {
                Busy = false;
                Changed();
            }
        });

    [LoggerMessage(Level = LogLevel.Error, Message = "Desktop operation failed.")]
    private static partial void LogOperationFailure(ILogger logger, Exception exception);
}
