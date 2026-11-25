using Avalonia.Controls;

namespace LazyTuiHub.Desktop;

public sealed record RemotePage(string Title, string Icon, Func<IServiceProvider, Control> Create);
