namespace FlatMonitor.Scraping.Common;

using HtmlAgilityPack;

/// <summary>
/// Shared extraction helpers for detail parsers across all sources.
/// Provides common patterns for extracting text and multi-pattern fallbacks; numbers are read by <see cref="GermanNumberParser"/>.
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

    public static string? ExtractFieldByMultiplePatterns(HtmlDocument doc, string label, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes != null && nodes.Count > 0)
            {
                var text = ExtractText(nodes[0]);
                if (text != null)
                    return text;
            }
        }
        return null;
    }

    public static bool HasNodes(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectNodes(xpath)?.Count > 0;

    public static bool HasValidStructure(HtmlDocument doc, params string[] xpaths)
        => xpaths.Any(xpath => HasNodes(doc, xpath));
}
