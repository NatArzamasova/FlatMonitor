namespace workInProgress.Scraping.ImmobilienScout24;

using HtmlAgilityPack;
using Microsoft.Playwright;
using workInProgress.Models;
using workInProgress.Scraping.Common;

/// <summary>
/// Scraper for immobilienscout24.de apartment listings.
/// Uses Playwright for browser automation (Google OAuth, robot guard), then parses listings with the generic ListingExtractor.
/// </summary>
public sealed class ImmobilienScout24Scraper : IScraper
{
    public const string SourceName = "ImmobilienScout24";

    private readonly ListingExtractor _extractor;
    private readonly IListingSelectors _selectors;

    public string Source => SourceName;

    public ImmobilienScout24Scraper()
    {
        _extractor = new ListingExtractor();
        _selectors = new ImmobilienScout24Selectors();
    }

    public async Task<IEnumerable<Flat>> ScrapeAsync() => await ScrapeAllListingsAsync();

    public async Task<List<Flat>> ScrapeAllListingsAsync(CancellationToken ct = default)
    {
        var allListings = new List<Flat>();

        try
        {
            // Use browser scraper to log in and get authenticated session
            var browserScraper = new ImmobilienScout24BrowserScraper();
            var (browser, page) = await browserScraper.LaunchAndAuthenticateAsync();

            if (browser == null || page == null)
            {
                Console.WriteLine("Failed to launch browser or authenticate.");
                return allListings;
            }

            // Extract and parse first page (already authenticated and loaded)
            Console.WriteLine("Extracting first page HTML...");
            var firstPageHtml = await page.ContentAsync();
            var firstPageListings = ParseListingsPage(firstPageHtml);
            allListings.AddRange(firstPageListings);
            Console.WriteLine($"Page 1: added {firstPageListings.Count} listings");

            // Use pagination fetcher for subsequent pages
            var pageFetcher = new ImmobilienScout24ListingsPageFetcher(browser, page);
            int pageNumber = 2;

            await foreach (var pageHtml in pageFetcher.FetchAllPagesHtmlAsync(ct))
            {
                var listings = ParseListingsPage(pageHtml);
                allListings.AddRange(listings);
                Console.WriteLine($"Page {pageNumber}: added {listings.Count} listings");
                pageNumber++;
            }

            Console.WriteLine($"ImmobilienScout24 scraper fetched {pageNumber - 1} pages with {allListings.Count} listings total.");

            await browserScraper.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error scraping ImmobilienScout24: {ex.Message}");
        }

        return allListings;
    }

    /// <summary>
    /// Parse all listing cards from a listings page HTML.
    /// </summary>
    private List<Flat> ParseListingsPage(string html)
    {
        var results = new List<Flat>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            Console.WriteLine($"Page HTML size: {html.Length} bytes");

            // Select all listing card containers
            var listingCards = doc.DocumentNode.SelectNodes(_selectors.ListingCardXPath);
            Console.WriteLine($"XPath '{_selectors.ListingCardXPath}' matched {listingCards?.Count ?? 0} cards");

            if (listingCards == null || listingCards.Count == 0)
            {
                Console.WriteLine("No listing cards found on page. Trying alternative selector...");
                // Try alternative selector
                listingCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card')]");
                Console.WriteLine($"Alternative selector matched {listingCards?.Count ?? 0} cards");

                if (listingCards == null || listingCards.Count == 0)
                {
                    return results;
                }
            }

            foreach (var card in listingCards)
            {
                try
                {
                    Console.WriteLine($"Card outerHTML (first 200 chars): {card.OuterHtml.Substring(0, Math.Min(200, card.OuterHtml.Length))}");
                    var listing = _extractor.ExtractListing(card, _selectors, SourceName);
                    if (listing != null)
                    {
                        Console.WriteLine($"Successfully parsed listing: {listing.ListingId}");
                        results.Add(listing);
                    }
                    else
                    {
                        Console.WriteLine("ListingExtractor returned null for this card");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to parse a listing card: {ex.Message}");
                }
            }

            Console.WriteLine($"Parsed {results.Count} listings from page.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing listings page: {ex.Message}");
        }

        return results;
    }
}
