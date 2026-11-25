namespace LazyTuiHub.Interaction;

public sealed class ClientSession(string? initialToken)
{
    private string? token = initialToken;
    public string? Token => Volatile.Read(ref token);
    public event EventHandler? Changed;

    public void SignIn(string issued)
    {
        if (Interlocked.CompareExchange(ref token, issued, null) is null)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        if (Interlocked.Exchange(ref token, null) is not null)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
