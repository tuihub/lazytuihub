namespace LazyTuiHub.Dao;

public sealed class DaoScope(string owner, DaoRegistry registry)
{
    public ICollection<DaoRequirement> Requirements { get; } = new List<DaoRequirement>();

    public DaoTable Table(string name) => new(owner, name);

    public IReadOnlyList<string> Problems() => Requirements.Select(registry.Problem).OfType<string>().ToArray();

    public T Get<T>(string name)
        where T : class =>
        registry.Resolve<T>(
            Requirements.Single(requirement => string.Equals(requirement.Name, name, StringComparison.Ordinal))
        );
}
