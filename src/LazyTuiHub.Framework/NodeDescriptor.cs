using System.Text.Json;

namespace LazyTuiHub.Framework;

public sealed record NodeDescriptor(string Type, string Module, Type? InputType, Type? OutputType);
