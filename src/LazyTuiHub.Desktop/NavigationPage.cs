using Avalonia.Controls;
using Avalonia.Media;

namespace LazyTuiHub.Desktop;

public sealed record NavigationPage(string Title, Func<Control> Create, Geometry? Icon);
