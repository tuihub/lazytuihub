using System.Reflection;
using System.Runtime.Loader;

namespace LazyTuiHub.Plugins;

public sealed class PluginAssemblies : IDisposable
{
    private readonly List<AssemblyDependencyResolver> resolvers = [];

    public PluginAssemblies() => AssemblyLoadContext.Default.Resolving += Resolve;

    public IApplicationPlugin Load(string path, string entryType)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Plugin assembly is missing.", path);
        resolvers.Add(new(path));
        var name = AssemblyName.GetAssemblyName(path);
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate =>
            AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), name)
        ) ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        return (IApplicationPlugin)Activator.CreateInstance(assembly.GetType(entryType, throwOnError: true)!)!;
    }

    private Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        foreach (var resolver in resolvers)
        {
            var path = resolver.ResolveAssemblyToPath(name);
            if (path is not null)
                return context.LoadFromAssemblyPath(path);
        }
        return null;
    }

    public void Dispose() => AssemblyLoadContext.Default.Resolving -= Resolve;
}
