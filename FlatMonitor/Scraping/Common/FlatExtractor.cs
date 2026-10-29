namespace FlatMonitor.Scraping.Common;

using System.Linq;
using FlatMonitor.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Generic flat extractor that works with any site via IFlatSelectors.
/// Contains all parsing logic; site-specific selectors are provided at runtime.
/// </summary>
public class FlatExtractor
{
    private readonly ILogger _logger;

    public FlatExtractor(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public Flat? ExtractFlat(HtmlNode cardNode, IFlatSelectors selectors, string source)
    {
        try
        {
            var id = ExtractId(cardNode, selectors);
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var title = ExtractText(cardNode, selectors.TitleXPath);
            var priceText = ExtractText(cardNode, selectors.PriceXPath);
            var sizeText = ExtractText(cardNode, selectors.SizeXPath);
            var address = ExtractText(cardNode, selectors.AddressXPath);
            var detailUrl = ExtractDetailUrl(cardNode, selectors, id);

            var price = ParsePrice(priceText);
            var size = ParseSize(sizeText);
            var rooms = selectors.RoomsXPath != null ? ParseRooms(ExtractText(cardNode, selectors.RoomsXPath)) : null;

            return new Flat
            {
                Source = source,
                ListingId = id,
                Url = detailUrl,
                Title = title ?? "Unknown",
                Address = address ?? "",
                PriceEur = price.HasValue ? (int)price.Value : null,
                LivingAreaSqm = size.HasValue ? (int)size.Value : null,
                RoomsCount = rooms,
                ScrapedAtUtc = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Source}: error extracting a flat from its listing card", source);
            return null;
        }
    }

    private string? ExtractId(HtmlNode cardNode, IFlatSelectors selectors)
    {
        string? idText = null;

        // Handle @data-obid attribute specially (direct attribute extraction)
        if (selectors.IdXPath == "@data-obid")
        {
            idText = cardNode.GetAttributeValue("data-obid", null);
        }
        else
        {
            idText = ExtractText(cardNode, selectors.IdXPath);
        }

        if (string.IsNullOrWhiteSpace(idText))
            return null;

        // Extract numeric ID from URLs like "/expose/12345" or IDs like "12345"
        var match = System.Text.RegularExpressions.Regex.Match(idText, @"\d+");
        return match.Success ? match.Value : idText;
    }

    private string? ExtractDetailUrl(HtmlNode cardNode, IFlatSelectors selectors, string? id = null)
    {
        var url = ExtractText(cardNode, selectors.DetailUrlXPath);
        if (string.IsNullOrWhiteSpace(url))
        {
            // If no URL found and we have an ID, construct URL from ID (e.g., /expose/{id})
            if (!string.IsNullOrWhiteSpace(id) && id.All(char.IsDigit))
                url = $"/expose/{id}";
            else
                return null;
        }

        // Convert relative URLs to absolute
        if (url.StartsWith("/"))
            url = selectors.BaseUrl.TrimEnd('/') + url;

        return url;
    }

    private string? ExtractText(HtmlNode node, string xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
            return null;

        try
        {
            var selectedNode = node.SelectSingleNode(xpath);
            return selectedNode?.InnerText?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private decimal? ParsePrice(string? priceText)
    {
        if (string.IsNullOrWhiteSpace(priceText))
            return null;

        // Extract numeric value from strings like "€ 1.250.000" or "1250000"
        var cleaned = System.Text.RegularExpressions.Regex.Replace(priceText, @"[^\d.,]", "");
        cleaned = cleaned.Replace(".", "").Replace(",", ".");

        if (decimal.TryParse(cleaned, out var price))
            return price;

        return null;
    }

    private decimal? ParseSize(string? sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
            return null;

        // Extract numeric value from strings like "84 m²" or "84m²"
        var match = System.Text.RegularExpressions.Regex.Match(sizeText, @"(\d+(?:[.,]\d+)?)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", "."), out var size))
            return size;

        return null;
    }

    private int? ParseRooms(string? roomsText)
    {
        if (string.IsNullOrWhiteSpace(roomsText))
            return null;

        // Extract numeric value from strings like "2 Zimmer" or "2"
        var match = System.Text.RegularExpressions.Regex.Match(roomsText, @"(\d+(?:[.,]\d+)?)");
        if (match.Success && int.TryParse(match.Groups[1].Value.Split(',', '.')[0], out var rooms))
            return rooms;

        return null;
    }
}
