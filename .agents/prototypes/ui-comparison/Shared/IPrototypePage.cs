namespace UiComparison;

public interface IPrototypePage<out TView>
{
    string Title { get; }
    TView CreateView();
}
