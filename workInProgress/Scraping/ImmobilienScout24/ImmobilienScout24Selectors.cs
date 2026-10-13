namespace workInProgress.Scraping.ImmobilienScout24;

using workInProgress.Scraping.Common;

/// <summary>
/// ImmobilienScout24-specific selectors for the ListingExtractor.
/// Maps immobilienscout24.de's React-rendered HTML structure to the common IListingSelectors interface.
/// Uses data-testid attributes for reliable selection across site updates.
/// </summary>
public class ImmobilienScout24Selectors : IListingSelectors
{
    /// <summary>Select all listing card containers on the listings page (with data-obid attribute).</summary>
    public string ListingCardXPath => "//div[contains(@class, 'listing-card') and @data-obid]";

    /// <summary>Extract listing ID from data-obid attribute.</summary>
    public string IdXPath => "@data-obid";

    /// <summary>Extract listing title/headline from headline element.</summary>
    /// <summary>Extract listing title (often in alt attribute of images or heading elements).</summary>
    public string TitleXPath => ".//img/@alt | .//h2//text() | .//h3//text()";

    /// <summary>Extract price from any text containing €.</summary>
    public string PriceXPath => ".//text()[contains(., '€')] | .//*[@data-testid='bottomBox']//text()";

    /// <summary>Extract size (m²) from attributes section or any text with m².</summary>
    public string SizeXPath => ".//text()[contains(., 'm²')] | .//*[@data-testid='attributes']//text()";

    /// <summary>Extract full address (typically in location/address elements).</summary>
    public string AddressXPath => ".//p[contains(@class, 'location')]//text() | .//div[contains(@class, 'address')]//text()";

    /// <summary>Extract number of rooms from attributes (e.g., "2 Zimmer").</summary>
    public string? RoomsXPath => ".//text()[contains(., 'Zimmer')] | .//*[@data-testid='attributes']//text()";

    /// <summary>Extract detail page URL from the expose link.</summary>
    public string DetailUrlXPath => ".//a[contains(@href, '/expose/')]/@href";

    /// <summary>Base URL for relative detail links.</summary>
    public string BaseUrl => "https://www.immobilienscout24.de";
}
