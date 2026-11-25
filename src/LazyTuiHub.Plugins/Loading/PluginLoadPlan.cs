namespace LazyTuiHub.Plugins;

internal static class PluginLoadPlan
{
    internal static void Configure(
        IEnumerable<PluginSpec> candidates,
        Func<PluginSpec, bool> configure,
        Action<PluginSpec, string> block
    )
    {
        var selected = candidates.ToDictionary(spec => spec.Id, StringComparer.Ordinal);
        var pending = new Dictionary<string, PluginSpec>(selected, StringComparer.Ordinal);
        foreach (var spec in selected.Values)
        {
            var problems = spec.Problems(id => selected.GetValueOrDefault(id)).ToArray();
            if (problems.Length == 0)
                continue;
            block(spec, string.Join(Environment.NewLine, problems));
            pending.Remove(spec.Id);
        }

        var configured = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var ready = pending.Values.FirstOrDefault(spec => !spec.RequiredIds.Any(pending.ContainsKey));
            if (ready is null)
            {
                var cycles = pending.Values.Where(spec => IsOnCycle(spec, pending)).ToArray();
                foreach (var spec in cycles)
                {
                    block(spec, $"Plugin dependency cycle involving '{spec.Id}'.");
                    pending.Remove(spec.Id);
                }
                continue;
            }
            pending.Remove(ready.Id);
            var missing = ready.RequiredIds.Where(id => !configured.Contains(id)).ToArray();
            if (missing.Length > 0)
                block(ready, $"Required plugins unavailable: {string.Join(", ", missing)}.");
            else if (configure(ready))
                configured.Add(ready.Id);
        }
    }

    // Distinguish cycle members from downstream plugins blocked by a missing requirement.
    private static bool IsOnCycle(PluginSpec origin, Dictionary<string, PluginSpec> pending)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var visit = new Stack<string>(origin.RequiredIds);
        while (visit.TryPop(out var id))
        {
            if (string.Equals(id, origin.Id, StringComparison.Ordinal))
                return true;
            if (!seen.Add(id) || !pending.TryGetValue(id, out var spec))
                continue;
            foreach (var dependency in spec.RequiredIds)
                visit.Push(dependency);
        }
        return false;
    }
}
