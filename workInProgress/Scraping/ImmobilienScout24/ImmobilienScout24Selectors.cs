namespace workInProgress.Scraping.ImmobilienScout24;

using workInProgress.Scraping.Common;

/// <summary>
/// ImmobilienScout24-specific selectors for the ListingExtractor.
/// Maps immobilienscout24.de's React-rendered HTML structure to the common IListingSelectors interface.
/// Uses data-testid attributes for reliable selection across site updates.
/// </summary>
public class ImmobilienScout24Selectors : IListingSelectors
{
    /// <summary>Select all listing card containers on the listings page.</summary>
    public string ListingCardXPath => "//div[@class='touchpoint-card']";

    /// <summary>Extract listing ID from detail URL: /expose/{ID}.</summary>
    public string IdXPath => ".//a[contains(@href, '/expose/')]/@href";

    /// <summary>Extract listing title/headline from headline element.</summary>
    public string TitleXPath => ".//*[@data-testid='headline']//text()";

    /// <summary>Extract price from bottomBox (contains price and key attributes).</summary>
    public string PriceXPath => ".//*[@data-testid='bottomBox']//text()[contains(., '€')]";

    /// <summary>Extract size (m²) from attributes section.</summary>
    public string SizeXPath => ".//*[@data-testid='attributes']//text()[contains(., 'm²')]";

    /// <summary>Extract full address from card (typically below headline).</summary>
    public string AddressXPath => ".//div[contains(@class, 'address')]//text() | .//p[contains(@class, 'location')]//text()";

    /// <summary>Extract number of rooms from attributes (e.g., "2 Zimmer").</summary>
    public string? RoomsXPath => ".//*[@data-testid='attributes']//text()[contains(., 'Zimmer')]";

    /// <summary>Extract detail page URL from the expose link.</summary>
    public string DetailUrlXPath => ".//a[contains(@href, '/expose/')]/@href";

    /// <summary>Base URL for relative detail links.</summary>
    public string BaseUrl => "https://www.immobilienscout24.de";
}
