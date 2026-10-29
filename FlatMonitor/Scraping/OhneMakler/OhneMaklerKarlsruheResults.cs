namespace FlatMonitor.Scraping.OhneMakler;

using FlatMonitor.Models;

/// <summary>
/// Where the Karlsruhe results end. The search lists flats in Karlsruhe first and flats from the surrounding area
/// after them, so the first card with a postcode outside Karlsruhe ends the scrape: that card and every later one,
/// on this page and on the following pages, are outside Karlsruhe.
/// </summary>
public static class OhneMaklerKarlsruheResults
{
    /// <summary>
    /// The cards before the first one outside Karlsruhe. Cards without a postcode are kept.
    /// <paramref name="firstOutside"/> is that first card, or <c>null</c> if all cards are in Karlsruhe.
    /// </summary>
    public static List<Flat> TakeUntilOutsideKarlsruhe(IEnumerable<Flat> cards, out Flat? firstOutside)
    {
        var inKarlsruhe = new List<Flat>();
        foreach (var card in cards)
        {
            if (!string.IsNullOrEmpty(card.PostalCode) && !OhneMaklerSelectors.KarlsruhePostcodes.Contains(card.PostalCode))
            {
                firstOutside = card;
                return inKarlsruhe;
            }
            inKarlsruhe.Add(card);
        }

        firstOutside = null;
        return inKarlsruhe;
    }
}
