using System.ComponentModel;

namespace LazyTuiHub.Games.Desktop;

public sealed class GameFieldDraft(string name, string? effective, string? upstream, string? source, bool personal)
    : INotifyPropertyChanged
{
    private string value = effective ?? string.Empty;
    private bool reset;
    public bool Multiline => name is "Description";
    public string Name => name;
    public string Source => reset ? "Source value" : source ?? "No source";
    public bool CanReset => personal && !reset;
    public bool Changed => reset || !string.Equals(value, effective ?? string.Empty, StringComparison.Ordinal);
    public string? Override => reset ? null : Value;
    public string Value
    {
        get => value;
        set
        {
            if (string.Equals(this.value, value, StringComparison.Ordinal))
                return;
            this.value = value;
            var wasReset = reset;
            reset = false;
            if (wasReset)
                PropertyChanged?.Invoke(this, new(string.Empty));
        }
    }

    public void Reset()
    {
        value = upstream ?? string.Empty;
        reset = true;
        PropertyChanged?.Invoke(this, new(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
