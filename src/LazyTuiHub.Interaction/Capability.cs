using System.Reflection;
using System.Text.Json;

namespace LazyTuiHub.Interaction;

public sealed class Capability
{
    private readonly MethodInfo method;
    private readonly Func<object> resolve;
    public string Name { get; }
    public string Description { get; }
    public CapabilityGroup Group { get; }
    public IReadOnlyList<CapabilityParameter> Parameters { get; }

    internal Capability(CapabilityGroup group, MethodInfo method, Func<object> resolve)
    {
        Group = group;
        this.method = method;
        this.resolve = resolve;
        Name = JsonNamingPolicy.KebabCaseLower.ConvertName(method.Name);
        Description = method.GetCustomAttribute<CapabilityAttribute>()!.Description;
        Parameters = method.GetParameters().Select(p => new CapabilityParameter(p)).ToArray();
    }

    public async Task<object?> InvokeAsync(
        Func<string, string?> input,
        Func<string, string> secret,
        InteractionOutput output,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        if (!Group.Available)
            throw new InvalidOperationException($"The {Group.Name} operations are unavailable for this session.");
        var arguments = Parameters.Select(p => p.Bind(input, secret, output, token)).ToArray();
        var value = method.Invoke(
            method.IsStatic ? null : resolve(),
            BindingFlags.DoNotWrapExceptions,
            null,
            arguments,
            null
        );
        if (value is not Task task)
            return value;
        await task.ConfigureAwait(false);
        return method.ReturnType.GetProperty("Result")?.GetValue(task);
    }

    public override string ToString() => $"{Group.Name} · {Name}";
}
