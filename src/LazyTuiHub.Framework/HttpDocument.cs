using System.Globalization;
using System.Runtime.CompilerServices;

namespace LazyTuiHub.Framework;

public sealed class HttpDocument(Uri uri, byte[] content, DataReference source)
{
    public Uri Uri { get; } = uri;
    public ReadOnlyMemory<byte> Content => content;
    public DataReference Source { get; } = source;

    public Stream OpenRead() => new MemoryStream(content, writable: false);
}
