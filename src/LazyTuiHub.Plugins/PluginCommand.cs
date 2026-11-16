using Microsoft.AspNetCore.Builder;

namespace LazyTuiHub.Plugins;

public sealed record PluginCommand(string Name, Func<WebApplication, string, CancellationToken, Task> Run);
