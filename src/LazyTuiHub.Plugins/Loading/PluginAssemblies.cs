using System.Reflection;
using System.Runtime.Loader;

namespace LazyTuiHub.Plugins;

public sealed class PluginAssemblies : IDisposable
{
    private readonly Dictionary<string, string> entries = new(StringComparer.OrdinalIgnoreCase);

    internal void Discover(string directory, IEnumerable<PluginSpec> specs)
    {
        foreach (var spec in specs)
        {
            var path = Path.GetFullPath(Path.Combine(directory, spec.Assembly));
            if (File.Exists(path))
                entries.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }
    }

    private readonly Dictionary<string, AssemblyDependencyResolver> resolvers = new(StringComparer.Ordinal);

    public PluginAssemblies() => AssemblyLoadContext.Default.Resolving += Resolve;

    public T Load<T>(string path, string entryType)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Plugin assembly is missing.", path);
        if (!resolvers.ContainsKey(path))
            resolvers.Add(path, new(path));
        var name = AssemblyName.GetAssemblyName(path);
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate =>
            AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), name)
        ) ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        return (T)Activator.CreateInstance(assembly.GetType(entryType, throwOnError: true)!)!;
    }

    private Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (name.Name is not null && entries.TryGetValue(name.Name, out var entry))
            return context.LoadFromAssemblyPath(entry);
        foreach (var resolver in resolvers.Values)
        {
            var path = resolver.ResolveAssemblyToPath(name);
            if (path is not null)
                return context.LoadFromAssemblyPath(path);
        }
        return null;
    }

    public void Dispose() => AssemblyLoadContext.Default.Resolving -= Resolve;
}
