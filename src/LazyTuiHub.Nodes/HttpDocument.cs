using System.Text.Json.Serialization;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Nodes;

public sealed class HttpDocument(Uri uri, byte[] content, DataReference source)
{
    [JsonConstructor]
    public HttpDocument(Uri uri, ReadOnlyMemory<byte> content, DataReference source)
        : this(uri, content.ToArray(), source) { }

    public Uri Uri { get; } = uri;
    public ReadOnlyMemory<byte> Content => content;
    public DataReference Source { get; } = source;

    public Stream OpenRead() => new MemoryStream(content, writable: false);
}
