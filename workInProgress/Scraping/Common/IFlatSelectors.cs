namespace workInProgress.Scraping.Common;

/// <summary>
/// Defines CSS selectors and XPath expressions for extracting flat data from HTML.
/// Each scraper implements this to describe how to find fields on its specific site.
/// </summary>
public interface IFlatSelectors
{
    /// <summary>XPath to select all flat card containers on a page.</summary>
    string ListingCardXPath { get; }

    /// <summary>XPath relative to card to extract flat ID (e.g., from /expose/12345).</summary>
    string IdXPath { get; }

    /// <summary>XPath relative to card to extract flat title/headline.</summary>
    string TitleXPath { get; }

    /// <summary>XPath relative to card to extract price text (e.g., "€ 1.250.000").</summary>
    string PriceXPath { get; }

    /// <summary>XPath relative to card to extract size in m² (e.g., "84 m²").</summary>
    string SizeXPath { get; }

    /// <summary>XPath relative to card to extract full address.</summary>
    string AddressXPath { get; }

    /// <summary>XPath relative to card to extract number of rooms (if available).</summary>
    string? RoomsXPath { get; }

    /// <summary>XPath relative to card to extract detail page URL (if relative, base URL is prepended).</summary>
    string DetailUrlXPath { get; }

    /// <summary>Base URL for relative detail links (e.g., "https://www.immobilienscout24.de").</summary>
    string BaseUrl { get; }
}
