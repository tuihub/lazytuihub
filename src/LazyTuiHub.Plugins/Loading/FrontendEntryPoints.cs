using LazyTuiHub.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace LazyTuiHub.Plugins;

public static class FrontendEntryPoints
{
    internal static IEnumerable<(PluginEntryPointKind Kind, PluginServices Registration)> Load(
        PluginAssemblies assemblies,
        string directory,
        PluginSpec spec,
        FrontendKind kind,
        bool remote
    )
    {
        foreach (var (role, type) in spec.EntryPoints.For(kind))
        {
            var registration = new PluginServices(spec.Id, remote);
            assemblies.Load<IPluginFrontend>(Path.Combine(directory, spec.Assembly), type).Configure(registration);
            yield return (role, registration);
        }
    }

    public static IReadOnlyList<(string Owner, DeclaredPage Page)> ConfigureRemote(
        PluginAssemblies assemblies,
        string directory,
        FrontendKind kind,
        IServiceCollection services,
        IEnumerable<string> reservedGroups,
        Action<string> report
    )
    {
        var pages = new List<(string, DeclaredPage)>();
        IReadOnlyList<PluginSpec> specs;
        try
        {
            specs = PluginManifest.Read(directory, report);
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
        {
            report(error.Message);
            return pages;
        }
        assemblies.Discover(directory, specs);
        var commands = reservedGroups.ToHashSet(StringComparer.Ordinal);
        var present = specs.Where(spec =>
        {
            if (File.Exists(Path.Combine(directory, spec.Assembly)))
                return true;
            report($"Missing plugin '{spec.Id}'.");
            return false;
        });
        PluginLoadPlan.Configure(present, Configure, (spec, reason) =>
            report($"Could not configure '{spec.Id}': {reason}")
        );
        return pages;

        bool Configure(PluginSpec spec)
        {
            try
            {
                var entries = Load(assemblies, directory, spec, kind, true).ToArray();
                var names = entries.SelectMany(entry => entry.Registration.Capabilities.Keys).ToArray();
                var conflict = names.FirstOrDefault(commands.Contains)
                    ?? names
                        .GroupBy(name => name, StringComparer.Ordinal)
                        .FirstOrDefault(group => group.Count() > 1)
                        ?.Key;
                if (conflict is not null)
                    throw new InvalidOperationException($"Command already registered: {conflict}.");
                commands.UnionWith(names);
                foreach (var (_, entry) in entries)
                {
                    foreach (var service in entry.Services)
                        services.Add(service);
                    foreach (var factory in entry.Capabilities.Values)
                        services.AddSingleton(factory);
                    pages.AddRange(entry.Pages.Select(page => (spec.Id, page)));
                }
                return true;
            }
            catch (Exception error)
            {
                report($"Could not configure '{spec.Id}': {error.GetBaseException().Message}");
                return false;
            }
        }
    }
}
