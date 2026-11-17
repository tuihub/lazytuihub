using Avalonia.Controls;

namespace LazyTuiHub.Desktop;

public sealed record NavigationPage(string Title, Func<Control> Create);
