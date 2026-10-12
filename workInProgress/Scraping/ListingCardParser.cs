namespace workInProgress.Scraping;

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using workInProgress.Models;

public sealed class ListingCardParser
{
    // Extract listing ID from URL like /immobilie/502814/
    private static readonly Regex ListingIdRegex = new(@"/immobilie/(\d+)/", RegexOptions.Compiled);

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

    private static string? ExtractTitle(HtmlNode card)
    {
        // Look for the listing title, usually in an <h2> or <h3> within the card.
        var titleNode = card.SelectSingleNode(".//h2 | .//h3");
        return titleNode?.InnerText?.Trim();
    }

    private static int? ExtractPrice(HtmlNode card)
    {
        // Look for price in the card. Price is typically in a prominent location.
        // Pattern: "€ 270.000" or "270000" etc.
        var priceNodes = card.SelectNodes(".//*[contains(text(), '€') or contains(text(), '.000')]");
        if (priceNodes != null)
        {
            foreach (var node in priceNodes)
            {
                var text = node.InnerText?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                // Extract digits, remove formatting
                var digits = System.Text.RegularExpressions.Regex.Replace(text, @"[^\d]", "");
                if (int.TryParse(digits, out int price))
                    return price;
            }
        }
        return null;
    }

    private static (string? PostalCode, string? District) ExtractLocation(HtmlNode card)
    {
        // Location is typically "12345 CityName (District)" format.
        var locationText = card.InnerText;
        if (string.IsNullOrEmpty(locationText))
            return (null, null);

        // Simple heuristic: look for 5-digit number (German postal code) + location name.
        var match = System.Text.RegularExpressions.Regex.Match(
            locationText, @"(\d{5})\s+(.+?)(?:\s*\((.+?)\))?(?:\s|$)");
        if (match.Success)
        {
            string postalCode = match.Groups[1].Value;
            string location = match.Groups[2].Value.Trim();
            string? district = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            return (postalCode, district ?? location);
        }

        return (null, null);
    }

    private static int? ExtractRooms(HtmlNode card)
    {
        // Rooms count, usually "3" or "2.5" Zimmer. We parse just the integer part.
        var text = card.InnerText;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+(?:,\d+)?)\s*(?:Zimmer|Zi)");
        if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ".").Split('.')[0], out int rooms))
            return rooms;
        return null;
    }

    private static int? ExtractArea(HtmlNode card)
    {
        // Living area, usually "82 m²" or "82m²".
        var text = card.InnerText;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*m[²2]");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int area))
            return area;
        return null;
    }
}
