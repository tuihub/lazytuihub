using System.Runtime.Loader;

namespace UiComparison;

public static class PluginLoader
{
    public static PluginResult<TView> Load<TView>(bool disabled, string entryType, string? assemblyPath = null)
    {
        if (disabled)
            return new(null, "Plugin disabled for this startup. Restart to enable it.");
        var path = assemblyPath ?? Path.Combine(AppContext.BaseDirectory, "plugins", "SamplePlugin.dll");
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            var page =
                (IPrototypePage<TView>)Activator.CreateInstance(assembly.GetType(entryType, throwOnError: true)!)!;
            return new(page, $"Loaded {assembly.GetName().Name} from plugins/SamplePlugin.dll");
        }
        catch (Exception error)
        {
            return new(null, $"Plugin unavailable: {error.GetType().Name}: {error.Message}");
        }
    }
}
