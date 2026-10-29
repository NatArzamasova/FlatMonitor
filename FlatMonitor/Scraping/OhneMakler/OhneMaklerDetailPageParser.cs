namespace FlatMonitor.Scraping.OhneMakler;

using System.Text.RegularExpressions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using HtmlAgilityPack;

/// <summary>Everything the scraper takes from an ohne-makler.net detail page; <c>null</c> where the page has no value.</summary>
public sealed record OhneMaklerDetailPage(
    string? Title,
    string? Address,
    double? Rooms,
    double? Area,
    int? Price,
    FlatDetails? Details);

/// <summary>
/// Parses ohne-makler.net detail pages, loading each page's HTML once.
/// Delegates selector definitions to OhneMaklerDetailSelectors for easy maintenance.
/// </summary>
public sealed partial class OhneMaklerDetailPageParser
{

    public OhneMaklerDetailPage Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return new OhneMaklerDetailPage(
            Title: ExtractTitle(doc),
            Address: ExtractAddress(doc),
            Rooms: ExtractRooms(doc),
            Area: ExtractArea(doc),
            Price: GermanNumberParser.ParseInteger(DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.PriceXPath)),
            Details: ParseDetailFields(doc));
    }

    public FlatDetails? ParseDetailFields(string html) => Parse(html).Details;

    private FlatDetails? ParseDetailFields(HtmlDocument doc)
    {
        if (!DetailExtractors.HasValidStructure(doc, OhneMaklerDetailSelectors.ValidStructurePatterns))
            return null;

        var details = new FlatDetails
        {
            PropertyType = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.PropertyTypeXPath),
            BuildYear = GermanNumberParser.ParseInteger(DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.BuildYearXPath)),
            Floor = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.FloorXPath)
                ?? DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.FloorAlternate1XPath)
                ?? DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.FloorAlternate2XPath),
            Condition = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.ConditionXPath),
            Heating = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.HeatingXPath),
            ServiceChargeEur = GermanNumberParser.ParseInteger(DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.ServiceChargeHausgeldXPath))
                ?? GermanNumberParser.ParseInteger(DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.ServiceChargeNebenkostenXPath)),
            EnergyCertificateType = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.EnergyCertificateTypeXPath),
            PrimaryEnergySource = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.PrimaryEnergySourceXPath),
            EnergyEfficiencyClass = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.EnergyEfficiencyClassXPath),
            EnergyConsumption = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.EnergyConsumptionXPath),
            ListingStatus = ExtractListingStatus(doc),
            CurrentUse = ExtractCurrentUse(doc),
            Availability = DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.AvailabilityXPath)
                ?? DetailExtractors.ExtractText(doc, OhneMaklerDetailSelectors.AvailabilityAlternateXPath),
        };

        return details;
    }

    /// <summary>The <c>h1</c> heading: the listing's title.</summary>
    private static string? ExtractTitle(HtmlDocument doc)
    {
        var text = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// The address follows the <c>h1</c> title, before the first <c>h2</c>/<c>h3</c> section header:
    /// a text node, or an <c>a</c>/<c>div</c>/<c>p</c> (on the live site a link to the map).
    /// </summary>
    private static string? ExtractAddress(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//h1")?.NextSibling;
        while (node != null)
        {
            if (node.Name == "h2" || node.Name == "h3")
                break;

            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = node.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 5) // Likely address, not just whitespace
                    return OhneMaklerAddressHelper.SanitizeAddress(NormalizeWhitespace(text));
            }
            else if (node.Name == "a" || node.Name == "div" || node.Name == "p")
            {
                var text = node.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 5 && text.Contains(' '))
                    return OhneMaklerAddressHelper.SanitizeAddress(NormalizeWhitespace(text));
            }

            node = node.NextSibling;
        }

        return null;
    }

    /// <summary>The first "&lt;n&gt; Zimmer" in the page text, with half rooms ("2,5 Zimmer" gives 2.5).</summary>
    private static double? ExtractRooms(HtmlDocument doc)
    {
        var match = Rooms().Match(doc.DocumentNode.InnerText);
        return match.Success ? GermanNumberParser.ToDouble(match.Groups[1].Value) : null;
    }

    /// <summary>
    /// The first "&lt;n&gt; m²" between 5 and 500, checking elements in document order. The outermost element
    /// (<c>html</c>) contains the whole text, so this is effectively the first plausible area anywhere on the page.
    /// </summary>
    private static double? ExtractArea(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//*[contains(., 'm²') or contains(., 'm2')]");
        if (nodes == null)
            return null;

        foreach (var node in nodes)
        {
            var match = Area().Match(node.InnerText?.Trim() ?? "");
            if (match.Success
                && GermanNumberParser.ToDouble(match.Groups[1].Value) is { } area
                && area >= 5 && area <= 500)
            {
                return area;
            }
        }

        return null;
    }

    private static string NormalizeWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"(\d+(?:,\d+)?)\s*(?:Zimmer|Zi)(?:\s|$)")]
    private static partial Regex Rooms();

    [GeneratedRegex(@"(\d+[.,]\d+|\d+)\s*(?:m²|m2)")]
    private static partial Regex Area();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private string? ExtractListingStatus(HtmlDocument doc)
    {
        var htmlText = doc.DocumentNode.InnerText;
        if (htmlText.Contains("Privatangebot", StringComparison.OrdinalIgnoreCase))
            return "Privatangebot";
        if (htmlText.Contains("Gewerbliches Angebot", StringComparison.OrdinalIgnoreCase))
            return "Gewerbliches Angebot";
        return null;
    }

    private string? ExtractCurrentUse(HtmlDocument doc)
        => DetailExtractors.ExtractFieldByMultiplePatterns(doc, "Aktuelle Nutzung",
            $"//tr[td/div[normalize-space()='Aktuelle Nutzung']]/td[2]/div",
            $"//span[normalize-space()='Aktuelle Nutzung']/preceding-sibling::span[@class]",
            $"//div[normalize-space()='Aktuelle Nutzung']/following-sibling::div")
        ?? DetailExtractors.ExtractFieldByMultiplePatterns(doc, "Nutzung",
            $"//tr[td/div[normalize-space()='Nutzung']]/td[2]/div",
            $"//span[normalize-space()='Nutzung']/preceding-sibling::span[@class]",
            $"//div[normalize-space()='Nutzung']/following-sibling::div");
}
