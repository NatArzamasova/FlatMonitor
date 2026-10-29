namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Text.RegularExpressions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Reads one listing card of an immobilienscout24.de results page into a <see cref="Flat"/>.
/// The card's listing ID is its <c>data-obid</c> attribute; the other fields come from <see cref="ImmobilienScout24Selectors"/>.
/// </summary>
public partial class ImmobilienScout24CardParser
{
    private readonly ILogger _logger;

    public ImmobilienScout24CardParser(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The card as a flat, or <c>null</c> if it has no listing ID or cannot be read.</summary>
    public Flat? Parse(HtmlNode cardNode)
    {
        try
        {
            var id = ExtractId(cardNode);
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return new Flat
            {
                Source = ImmobilienScout24Scraper.SourceName,
                ListingId = id,
                Url = ExtractDetailUrl(cardNode, id),
                Title = ExtractText(cardNode, ImmobilienScout24Selectors.TitleXPath) ?? "Unknown",
                Address = ExtractText(cardNode, ImmobilienScout24Selectors.AddressXPath) ?? "",
                PriceEur = GermanNumberParser.ParseInteger(ExtractText(cardNode, ImmobilienScout24Selectors.PriceXPath)),
                LivingAreaSqm = GermanNumberParser.ParseDecimal(ExtractText(cardNode, ImmobilienScout24Selectors.SizeXPath)),
                RoomsCount = GermanNumberParser.ParseDecimal(ExtractText(cardNode, ImmobilienScout24Selectors.RoomsXPath)),
                ScrapedAtUtc = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImmobilienScout24: error extracting a flat from its listing card");
            return null;
        }
    }

    private string? ExtractId(HtmlNode cardNode)
    {
        var idText = cardNode.GetAttributeValue(ImmobilienScout24Selectors.IdAttribute, null);
        if (string.IsNullOrWhiteSpace(idText))
            return null;

        // The numeric ID, also from values like "/expose/12345"
        var match = Digits().Match(idText);
        return match.Success ? match.Value : idText;
    }

    /// <summary>
    /// The expose link's <c>href</c> as an absolute URL without query or fragment (tracking parameters, "#/"),
    /// so the same listing always gets the same URL. Without a link, the URL is built from a numeric ID.
    /// </summary>
    private string? ExtractDetailUrl(HtmlNode cardNode, string id)
    {
        var baseUri = new Uri(ImmobilienScout24Selectors.BaseUrl);
        var href = HtmlEntity.DeEntitize(
            cardNode.SelectSingleNode(ImmobilienScout24Selectors.DetailLinkXPath)?.GetAttributeValue("href", null)?.Trim() ?? "");

        if (!string.IsNullOrEmpty(href) && Uri.TryCreate(baseUri, href, out var linkUri))
            return linkUri.GetLeftPart(UriPartial.Path);

        return id.All(char.IsDigit) ? new Uri(baseUri, $"/expose/{id}").AbsoluteUri : null;
    }

    private static string? ExtractText(HtmlNode node, string xpath) =>
        node.SelectSingleNode(xpath)?.InnerText?.Trim();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
