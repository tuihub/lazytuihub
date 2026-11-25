using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace LazyTuiHub.Interaction;

public sealed class CapabilityParameter
{
    private readonly ParameterInfo parameter;
    public string Name { get; }
    public bool IsSecret => parameter.ParameterType == typeof(SecretInput);
    public bool IsInput =>
        parameter.ParameterType != typeof(InteractionOutput) && parameter.ParameterType != typeof(CancellationToken);
    public bool Optional => parameter.HasDefaultValue;
    public Type ValueType => parameter.ParameterType;

    internal CapabilityParameter(ParameterInfo parameter)
    {
        this.parameter = parameter;
        Name = JsonNamingPolicy.KebabCaseLower.ConvertName(parameter.Name!);
    }

    internal object? Bind(
        Func<string, string?> input,
        Func<string, string> secret,
        InteractionOutput output,
        CancellationToken token
    )
    {
        if (ValueType == typeof(InteractionOutput))
            return output;
        if (ValueType == typeof(CancellationToken))
            return token;
        if (IsSecret)
            return new SecretInput(() => secret(Name));
        if (input(Name) is not { } text)
            return Optional ? parameter.DefaultValue : throw new ArgumentException($"Provide --{Name}.", nameof(input));
        if (ValueType == typeof(string))
            return text;
        if (ValueType == typeof(bool))
            return bool.Parse(text);
        if (ValueType == typeof(TimeSpan))
            return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
        if (ValueType == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
        return JsonSerializer.Deserialize(text, ValueType)
            ?? throw new FormatException($"Provide a JSON value for {Name}.");
    }
}
