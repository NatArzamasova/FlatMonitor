namespace FlatMonitor.Scraping.ImmobilienScout24;

/// <summary>
/// Where <see cref="ImmobilienScout24CardParser"/> finds each field on immobilienscout24.de's React-rendered results page.
/// Uses data-testid attributes for reliable selection across site updates.
/// </summary>
public static class ImmobilienScout24Selectors
{
    /// <summary>Select all listing card containers on the listings page (with data-obid attribute).</summary>
    public const string ListingCardXPath = "//div[contains(@class, 'listing-card') and @data-obid]";

    /// <summary>The card attribute holding the listing ID.</summary>
    public const string IdAttribute = "data-obid";

    /// <summary>Extract listing title (often in alt attribute of images or heading elements).</summary>
    public const string TitleXPath = ".//img/@alt | .//h2//text() | .//h3//text()";

    /// <summary>Extract price from any text containing €.</summary>
    public const string PriceXPath = ".//text()[contains(., '€')] | .//*[@data-testid='bottomBox']//text()";

    /// <summary>Extract size (m²) from attributes section or any text with m².</summary>
    public const string SizeXPath = ".//text()[contains(., 'm²')] | .//*[@data-testid='attributes']//text()";

    /// <summary>Extract full address (typically in location/address elements).</summary>
    public const string AddressXPath = ".//p[contains(@class, 'location')]//text() | .//div[contains(@class, 'address')]//text()";

    /// <summary>Extract number of rooms from attributes (e.g., "2 Zimmer").</summary>
    public const string RoomsXPath = ".//text()[contains(., 'Zimmer')] | .//*[@data-testid='attributes']//text()";

    /// <summary>The expose link; its <c>href</c> is the detail page URL. (Not ".../@href": HtmlAgilityPack would select the element.)</summary>
    public const string DetailLinkXPath = ".//a[contains(@href, '/expose/')]";

    /// <summary>Base URL for relative detail links.</summary>
    public const string BaseUrl = "https://www.immobilienscout24.de";
}
