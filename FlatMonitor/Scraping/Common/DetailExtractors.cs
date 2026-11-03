namespace FlatMonitor.Scraping.Common;

using HtmlAgilityPack;

/// <summary>
/// Shared extraction helpers for detail parsers across all sources.
/// Provides common patterns for extracting text and attributes; numbers are read by <see cref="GermanNumberParser"/>.
/// A missing node gives <c>null</c>; an invalid XPath throws, as it is a programming error the parser tests catch.
/// </summary>
public static class DetailExtractors
{
    public static string? ExtractText(HtmlNode? node)
        => node?.InnerText?.Trim() is { Length: > 0 } text ? text : null;

    public static string? ExtractText(HtmlDocument doc, string xpath)
        => ExtractText(doc.DocumentNode.SelectSingleNode(xpath));

    public static string? ExtractAttribute(HtmlDocument doc, string xpath, string attributeName)
    {
        var value = doc.DocumentNode.SelectSingleNode(xpath)?.GetAttributeValue(attributeName, null)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// The page's <c>og:image</c> meta tag: the listing's title photo on all three portals. <c>null</c> without one or
    /// if it is no absolute http(s) URL.
    /// </summary>
    public static string? ExtractOpenGraphImage(HtmlDocument doc)
    {
        var content = ExtractAttribute(doc, "//meta[@property='og:image']", "content");
        return content != null
            && Uri.TryCreate(HtmlEntity.DeEntitize(content), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? uri.AbsoluteUri
                : null;
    }

    public static bool HasNodes(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectNodes(xpath)?.Count > 0;

    public static bool HasValidStructure(HtmlDocument doc, params string[] xpaths)
        => xpaths.Any(xpath => HasNodes(doc, xpath));
}
