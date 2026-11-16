using System.Globalization;
using System.Runtime.CompilerServices;

namespace LazyTuiHub.Framework;

public sealed record FetchRequest(Uri Uri, DataReference Source);
