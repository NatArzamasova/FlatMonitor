namespace workInProgress.Storage;

using HtmlAgilityPack;

/// <summary>
/// Shared extraction helpers for detail parsers across all sources.
/// Provides common patterns for extracting text, integers, and multi-pattern fallbacks.
/// </summary>
public static class DetailExtractors
{
    public static string? ExtractText(HtmlNode? node)
        => node?.InnerText?.Trim() is { Length: > 0 } text ? text : null;

    public static string? ExtractText(HtmlDocument doc, string xpath)
    {
        try
        {
            var text = doc.DocumentNode.SelectSingleNode(xpath)?.InnerText?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
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

    public static int? ExtractIntField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = System.Text.RegularExpressions.Regex.Replace(value, @"[^\d]", "");
        return int.TryParse(digits, out var result) ? result : null;
    }

    public static bool HasNodes(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectNodes(xpath)?.Count > 0;

    public static bool HasValidStructure(HtmlDocument doc, params string[] xpaths)
        => xpaths.Any(xpath => HasNodes(doc, xpath));
}
