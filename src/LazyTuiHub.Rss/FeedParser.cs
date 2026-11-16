using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LazyTuiHub.Framework;

namespace LazyTuiHub.Rss;

public static class FeedParser
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";

    public static IEnumerable<ArticleDraft> Parse(HttpDocument document, CancellationToken cancellationToken = default)
    {
        using var stream = document.OpenRead();
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
        );
        reader.MoveToContent();
        var atom =
            reader.LocalName is "feed" && reader.NamespaceURI.Equals(Atom.NamespaceName, StringComparison.Ordinal);
        if (!atom && reader.LocalName is not "rss")
            throw new FormatException("Expected an RSS 2.0 or Atom feed.");
        while (!reader.EOF)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (
                reader.NodeType == XmlNodeType.Element
                && (
                    (
                        atom
                        && reader.LocalName is "entry"
                        && reader.NamespaceURI.Equals(Atom.NamespaceName, StringComparison.Ordinal)
                    ) || (!atom && reader.LocalName is "item" && reader.NamespaceURI.Length == 0)
                )
            )
            {
                var element = (XElement)XNode.ReadFrom(reader);
                yield return ParseEntry(element, document, atom);
            }
            else
                reader.Read();
        }
    }

    private static ArticleDraft ParseEntry(XElement entry, HttpDocument document, bool atom)
    {
        var ns = atom ? Atom : XNamespace.None;
        var title = entry.Element(ns + "title")?.Value ?? string.Empty;
        var link = atom
            ? entry
                .Elements(Atom + "link")
                .FirstOrDefault(element => (string?)element.Attribute("rel") is null or "alternate")
                ?.Attribute("href")
                ?.Value
            : entry.Element("link")?.Value;
        var url =
            !string.IsNullOrWhiteSpace(link) && Uri.TryCreate(document.Uri, link, out var resolved)
                ? resolved.AbsoluteUri
                : string.Empty;
        var body = (atom ? entry.Element(Atom + "content") : entry.Element(Content + "encoded"))
            ?? entry.Element(ns + (atom ? "summary" : "description"));
        var content = body is { HasElements: true }
            ? string.Concat(body.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)))
            : body?.Value ?? string.Empty;
        var published = entry.Element(ns + (atom ? "published" : "pubDate"))?.Value
            ?? entry.Element(Atom + "updated")?.Value;
        var id = entry.Element(ns + (atom ? "id" : "guid"))?.Value;
        if (string.IsNullOrWhiteSpace(id))
            id = string.IsNullOrEmpty(url) ? StableContentId(title, published, content) : url;
        return new(
            document.Source.Key,
            id,
            title,
            url,
            content,
            DateTimeOffset.TryParse(
                published,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var time
            )
                ? time.ToUniversalTime()
                : null
        );
    }

    private static string StableContentId(string title, string? published, string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{title}\0{published}\0{content}")));
}
