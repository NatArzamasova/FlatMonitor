namespace FlatMonitor.Scraping.OhneMakler;

using System.Globalization;
using System.Text.RegularExpressions;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Scraping.Common;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Everything the scraper takes from an ohne-makler.net detail page; <c>null</c> where the page has no value.</summary>
public sealed record OhneMaklerDetailPage(
    string? Title,
    string? Address,
    int? Rooms,
    double? Area,
    int? Price,
    FlatDetails? Details);

/// <summary>
/// Parses ohne-makler.net detail pages, loading each page's HTML once.
/// Delegates selector definitions to OhneMaklerDetailSelectors for easy maintenance.
/// </summary>
public sealed class OhneMaklerDetailPageParser : IDetailParser
{
    private static readonly Regex RoomsRegex = new(@"(\d+(?:,\d+)?)\s*(?:Zimmer|Zi)(?:\s|$)", RegexOptions.Compiled);
    private static readonly Regex AreaRegex = new(@"(\d+[.,]\d+|\d+)\s*(?:m²|m2)", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly OhneMaklerDetailSelectors _selectors;
    private readonly ILogger _logger;

    public OhneMaklerDetailPageParser(ILogger? logger = null)
    {
        _selectors = new OhneMaklerDetailSelectors();
        _logger = logger ?? NullLogger.Instance;
    }

    public OhneMaklerDetailPage Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Each value on its own: one that cannot be read must not cost the others
        return new OhneMaklerDetailPage(
            Title: TryExtract(ExtractTitle, doc),
            Address: TryExtract(ExtractAddress, doc),
            Rooms: TryExtract(ExtractRooms, doc),
            Area: TryExtract(ExtractArea, doc),
            Price: TryExtract(d => DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(d, _selectors.PriceXPath)), doc),
            Details: ParseDetailFields(doc));
    }

    public FlatDetails? ParseDetailFields(string html) => Parse(html).Details;

    private FlatDetails? ParseDetailFields(HtmlDocument doc)
    {
        try
        {
            if (!DetailExtractors.HasValidStructure(doc, _selectors.ValidStructurePatterns))
                return null;

            var details = new FlatDetails
            {
                PropertyType = DetailExtractors.ExtractText(doc, _selectors.PropertyTypeXPath),
                BuildYear = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.BuildYearXPath)),
                Floor = DetailExtractors.ExtractText(doc, _selectors.FloorXPath)
                    ?? DetailExtractors.ExtractText(doc, _selectors.FloorAlternate1XPath)
                    ?? DetailExtractors.ExtractText(doc, _selectors.FloorAlternate2XPath),
                Condition = DetailExtractors.ExtractText(doc, _selectors.ConditionXPath),
                Heating = DetailExtractors.ExtractText(doc, _selectors.HeatingXPath),
                ServiceChargeEur = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.ServiceChargeHausgeldXPath))
                    ?? DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.ServiceChargeNebenkostenXPath)),
                EnergyCertificateType = DetailExtractors.ExtractText(doc, _selectors.EnergyCertificateTypeXPath),
                PrimaryEnergySource = DetailExtractors.ExtractText(doc, _selectors.PrimaryEnergySourceXPath),
                EnergyEfficiencyClass = DetailExtractors.ExtractText(doc, _selectors.EnergyEfficiencyClassXPath),
                EnergyConsumption = DetailExtractors.ExtractText(doc, _selectors.EnergyConsumptionXPath),
                ListingStatus = ExtractListingStatus(doc),
                CurrentUse = ExtractCurrentUse(doc),
                Availability = DetailExtractors.ExtractText(doc, _selectors.AvailabilityXPath)
                    ?? DetailExtractors.ExtractText(doc, _selectors.AvailabilityAlternateXPath),
            };

            return details;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OhneMakler: failed to parse detail page");
            return null;
        }
    }

    private T? TryExtract<T>(Func<HtmlDocument, T?> extract, HtmlDocument doc)
    {
        try
        {
            return extract(doc);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OhneMakler: could not extract a value from a detail page");
            return default;
        }
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

    /// <summary>The first "&lt;n&gt; Zimmer" in the page text. Whole rooms only: "2,5 Zimmer" gives 2 (see 3.5).</summary>
    private static int? ExtractRooms(HtmlDocument doc)
    {
        var match = RoomsRegex.Match(doc.DocumentNode.InnerText);
        return match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ".").Split('.')[0], out int rooms)
            ? rooms
            : null;
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
            var match = AreaRegex.Match(node.InnerText?.Trim() ?? "");
            if (match.Success
                && double.TryParse(match.Groups[1].Value.Replace(",", "."), CultureInfo.InvariantCulture, out double area)
                && area >= 5 && area <= 500)
            {
                return area;
            }
        }

        return null;
    }

    private static string NormalizeWhitespace(string text) => WhitespaceRegex.Replace(text, " ").Trim();

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
