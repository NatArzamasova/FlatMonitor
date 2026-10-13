namespace workInProgress.Scraping;

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using workInProgress.Models;

public sealed class ListingCardParser
{
    // Extract listing ID from URL like /immobilie/502814/
    private static readonly Regex ListingIdRegex = new(@"/immobilie/(\d+)/", RegexOptions.Compiled);
    private static readonly Regex RoomsRegex = new(@"(\d+(?:,\d+)?)\s*(?:Zimmer|Zi)", RegexOptions.Compiled);
    private static readonly Regex AreaRegex = new(@"(\d+)\s*m[²2]", RegexOptions.Compiled);
    private static readonly Regex LocationRegex = new(@"(\d{5})\s+(.+?)(?:\s*\((.+?)\))?(?:\s|$)", RegexOptions.Compiled);

    public List<(Listing Listing, string DetailPageUrl)> ParsePage(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var listingLinks = doc.DocumentNode.SelectNodes(Selectors.ListingLinkXPath);
        var results = new List<(Listing, string)>();

        if (listingLinks == null || listingLinks.Count == 0)
            return results;

        foreach (var link in listingLinks)
        {
            try
            {
                var href = link.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                var match = ListingIdRegex.Match(href);
                if (!match.Success)
                    continue;

                string listingId = match.Groups[1].Value;

                // Find the card container (parent elements) to extract title, price, location, rooms, area.
                var card = GetCardContainer(link);
                if (card == null)
                    continue;

                string? title = ExtractTitle(card);
                int? priceEur = ExtractPrice(card);
                (string? PostalCode, string? District) = ExtractLocation(card);
                int? rooms = ExtractRooms(card);
                int? area = ExtractArea(card);

                var listing = new Listing
                {
                    ListingId = listingId,
                    Url = href,
                    Title = title,
                    PostalCode = PostalCode,
                    District = District,
                    PriceEur = priceEur,
                    RoomsCount = rooms,
                    LivingAreaSqm = area,
                    ScrapedAtUtc = DateTime.UtcNow.ToString("O"),
                };

                results.Add((listing, href));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse a listing card: {ex.Message}");
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

    private static int? ExtractInteger(string? text, Regex? pattern = null, int groupIndex = 1)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        if (pattern == null)
        {
            // Remove all non-digits (for price with thousand separators like "€ 270.000" → "270000")
            var digitsOnly = Regex.Replace(text, @"[^\d]", "");
            if (int.TryParse(digitsOnly, out int result))
                return result;
            return null;
        }

        // Use regex to extract with context (for rooms, area)
        var match = pattern.Match(text);
        if (!match.Success)
            return null;

        var value = match.Groups[groupIndex].Value.Replace(",", ".");
        if (int.TryParse(value.Split('.')[0], out int result2))
            return result2;

        return null;
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
                var price = ExtractInteger(node.InnerText?.Trim());
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

        var match = LocationRegex.Match(locationText);
        if (match.Success)
        {
            string postalCode = match.Groups[1].Value;
            string location = match.Groups[2].Value.Trim();
            string? district = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            return (postalCode, district ?? location);
        }

        return (null, null);
    }

    private static int? ExtractRooms(HtmlNode card) =>
        ExtractInteger(card.InnerText, RoomsRegex);

    private static int? ExtractArea(HtmlNode card) =>
        ExtractInteger(card.InnerText, AreaRegex);
}
