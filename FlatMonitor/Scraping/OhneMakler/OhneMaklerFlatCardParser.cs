namespace FlatMonitor.Scraping.OhneMakler;

using System.Text.RegularExpressions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed partial class OhneMaklerFlatCardParser
{

    private readonly ILogger _logger;

    public OhneMaklerFlatCardParser(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public List<Flat> ParsePage(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var listingLinks = doc.DocumentNode.SelectNodes(OhneMaklerSelectors.ListingLinkXPath);
        var results = new List<Flat>();

        if (listingLinks == null || listingLinks.Count == 0)
            return results;

        foreach (var link in listingLinks)
        {
            try
            {
                var href = link.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                var match = ListingId().Match(href);
                if (!match.Success)
                    continue;

                string listingId = match.Groups[1].Value;
                // Absolute from the start, so nothing later has to resolve it
                var url = new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), href).AbsoluteUri;

                // Find the card container (parent elements) to extract title, price, location, rooms, area.
                var card = GetCardContainer(link);
                if (card == null)
                    continue;

                string? title = ExtractTitle(card);
                int? priceEur = ExtractPrice(card);
                (string? PostalCode, string? District) = ExtractLocation(card);
                double? rooms = ExtractRooms(card);
                double? area = ExtractArea(card);

                var flat = new Flat
                {
                    ListingId = listingId,
                    Url = url,
                    Title = title,
                    PostalCode = PostalCode,
                    District = District,
                    PriceEur = priceEur,
                    // The site shows no struck-out price, so the original price is the price
                    OriginalPriceEur = priceEur,
                    RoomsCount = rooms,
                    LivingAreaSqm = area,
                    ScrapedAtUtc = DateTime.UtcNow,
                    Source = OhneMaklerScraper.SourceName,
                };

                results.Add(flat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OhneMakler: failed to parse a listing card");
            }
        }

        return results;
    }

    private static HtmlNode? GetCardContainer(HtmlNode link)
    {
        var node = link.ParentNode;
        while (node != null)
        {
            // Look for a reasonable card container (e.g., a div that groups the listing info).
            // Since the site is Tailwind-heavy with no semantic classes, we walk up until we find
            // something that looks like a card (a reasonable height/depth of DOM).
            // For now, we'll just use the first few ancestor divs and extract fields from there.
            if (node.Name == "div" && node.HasChildNodes)
                return node;
            node = node.ParentNode;
            if (node != null && node.Name == "html")
                break;
        }
        return link;
    }

    private static string? ExtractTitle(HtmlNode card)
    {
        // Look for the listing title, usually in an <h2> or <h3> within the card.
        var titleNode = card.SelectSingleNode(".//h2 | .//h3");
        return titleNode?.InnerText?.Trim();
    }

    private static int? ExtractPrice(HtmlNode card)
    {
        var priceNodes = card.SelectNodes(".//*[contains(text(), '€') or contains(text(), '.000')]");
        if (priceNodes != null)
        {
            foreach (var node in priceNodes)
            {
                var price = GermanNumberParser.ParseInteger(node.InnerText?.Trim());
                if (price.HasValue)
                    return price;
            }
        }
        return null;
    }

    private static (string? PostalCode, string? District) ExtractLocation(HtmlNode card)
    {
        var locationText = card.InnerText;
        if (string.IsNullOrEmpty(locationText))
            return (null, null);

        var match = Location().Match(locationText);
        if (match.Success)
        {
            string postalCode = match.Groups[1].Value;
            string location = match.Groups[2].Value.Trim();
            string? district = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            return (postalCode, district ?? location);
        }

        return (null, null);
    }

    /// <summary>The first "&lt;n&gt; Zimmer" on the card, with half rooms ("2,5 Zimmer" gives 2.5).</summary>
    private static double? ExtractRooms(HtmlNode card) =>
        Rooms().Match(card.InnerText) is { Success: true } match ? GermanNumberParser.ToDouble(match.Groups[1].Value) : null;

    /// <summary>The first "&lt;n&gt; m²" on the card, with decimals ("74,5 m²" gives 74.5).</summary>
    private static double? ExtractArea(HtmlNode card) =>
        Area().Match(card.InnerText) is { Success: true } match ? GermanNumberParser.ToDouble(match.Groups[1].Value) : null;

    // Listing ID from a URL like /immobilie/502814/
    [GeneratedRegex(@"/immobilie/(\d+)/")]
    private static partial Regex ListingId();

    [GeneratedRegex(@"(\d+(?:,\d+)?)\s*(?:Zimmer|Zi)")]
    private static partial Regex Rooms();

    [GeneratedRegex(@"(\d+(?:,\d+)?)\s*m[²2]")]
    private static partial Regex Area();

    [GeneratedRegex(@"(\d{5})\s+(.+?)(?:\s*\((.+?)\))?(?:\s|$)")]
    private static partial Regex Location();
}
