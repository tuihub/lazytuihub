namespace LazyTuiHub.Dao;

public sealed class DaoRegistry
{
    private readonly Dictionary<DaoCapability, (string Owner, object Value)> services = [];

    public IReadOnlyList<DaoPublication> List() =>
        services.Select(item => new DaoPublication(item.Value.Owner, item.Key)).ToArray();

    public void Validate(IEnumerable<DaoService> contributions)
    {
        var keys = contributions.Select(service => service.Capability).ToArray();
        if (keys.Distinct().Count() != keys.Length || keys.Any(services.ContainsKey))
            throw new ArgumentException("A DAO capability is already published.", nameof(contributions));
    }

    public void Publish(string owner, IEnumerable<DaoService> contributions)
    {
        var snapshot = contributions.ToArray();
        Validate(snapshot);
        foreach (var service in snapshot)
            services.Add(service.Capability, (owner, service.Implementation));
    }

    public string? Problem(DaoRequirement requirement)
    {
        var count = Candidates(requirement).Count();
        return count switch
        {
            1 => null,
            0 =>
                $"DAO '{requirement.Name}' requires {requirement.Mode}/{requirement.Contract}, instance '{requirement.Instance}', backend '{requirement.Backend ?? "any"}'; no active provider matches.",
            _ => $"DAO '{requirement.Name}' matches multiple providers; select a backend explicitly.",
        };
    }

    public T Resolve<T>(DaoRequirement requirement)
        where T : class
    {
        if (Problem(requirement) is { } problem)
            throw new InvalidOperationException(problem);
        var value = Candidates(requirement).Single().Value.Value;
        return value as T
            ?? throw new InvalidOperationException(
                $"DAO '{requirement.Name}' does not implement {typeof(T).FullName}."
            );
    }

    private IEnumerable<KeyValuePair<DaoCapability, (string Owner, object Value)>> Candidates(
        DaoRequirement requirement
    ) =>
        services.Where(item =>
            item.Key.Mode == requirement.Mode
            && string.Equals(item.Key.Contract, requirement.Contract, StringComparison.Ordinal)
            && string.Equals(item.Key.Instance, requirement.Instance, StringComparison.Ordinal)
            && (
                requirement.Backend is null
                || string.Equals(item.Key.Backend, requirement.Backend, StringComparison.Ordinal)
            )
        );
}
