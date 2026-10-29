namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using HtmlAgilityPack;
using Microsoft.Playwright;

/// <summary>
/// Scraper for immobilienscout24.de apartment flats.
/// Uses Playwright for browser automation (Google OAuth, robot guard), then parses flats with the generic FlatExtractor.
/// Processes each flat's details immediately as pages are fetched.
/// Saves flats to database in batches to avoid memory bloat on large datasets.
/// </summary>
public sealed class ImmobilienScout24Scraper : FlatScraper
{
    public const string SourceName = "ImmobilienScout24";

    private readonly FlatExtractor _extractor;
    private readonly IFlatSelectors _selectors;
    private readonly string _htmlSnapshotsDir;
    private IBrowser? _browser;
    private IPage? _page;
    private ImmobilienScout24DetailScraper? _detailScraper;
    private ImmobilienScout24DetailParser? _detailParser;
    private ImmobilienScout24FlatsPageFetcher? _pageFetcher;

    public override string Source => SourceName;

    public ImmobilienScout24Scraper(StorageOptions storage)
    {
        _extractor = new FlatExtractor();
        _selectors = new ImmobilienScout24Selectors();
        _htmlSnapshotsDir = Path.Combine(storage.DataDirectory, "snapshots", "html", SourceName);
    }

    protected override async Task InitializeAsync(CancellationToken ct)
    {
        var browserScraper = new ImmobilienScout24BrowserScraper();
        var (browser, page) = await browserScraper.LaunchAndAuthenticateAsync();

        if (browser == null || page == null)
        {
            throw new InvalidOperationException("Failed to launch browser or authenticate.");
        }

        _browser = browser;
        _page = page;
        _detailScraper = new ImmobilienScout24DetailScraper(page, _htmlSnapshotsDir);
        _detailParser = new ImmobilienScout24DetailParser();
        _pageFetcher = new ImmobilienScout24FlatsPageFetcher(browser, page);
    }

    protected override Task ClearPreviousSnapshotsAsync(CancellationToken ct)
    {
        try
        {
            var htmlDir = _htmlSnapshotsDir;

            if (Directory.Exists(htmlDir))
            {
                var files = Directory.GetFiles(htmlDir, "*.html");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                Console.WriteLine($"Cleared {files.Length} previous HTML snapshots from {htmlDir}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not clear previous HTML snapshots: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    protected override async IAsyncEnumerable<List<Flat>> FetchAndParseAllPagesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        if (_page == null)
        {
            yield break;
        }

        // Fetch and parse first page
        Console.WriteLine("Processing first page...");
        var firstPageHtml = await _page.ContentAsync();

        // Debug: save raw HTML for inspection if it's suspiciously small
        if (firstPageHtml.Length < 10000)
        {
            var debugPath = Path.Combine(Path.GetTempPath(), $"immobilienscout24_debug_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            await File.WriteAllTextAsync(debugPath, firstPageHtml);
            Console.WriteLine($"Debug: Page HTML is only {firstPageHtml.Length} bytes, saved to {debugPath}");
        }

        var firstPageListings = ParseListingsPage(firstPageHtml);
        yield return firstPageListings;

        // Fetch and parse remaining pages
        if (_pageFetcher != null)
        {
            await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
            {
                var listings = ParseListingsPage(pageHtml);
                yield return listings;
            }
        }
    }

    protected override async Task<Flat> EnrichListingWithDetailsAsync(Flat baseListing, CancellationToken ct)
    {
        if (_detailScraper == null || _detailParser == null)
        {
            return baseListing;
        }

        if (string.IsNullOrEmpty(baseListing.Url))
        {
            Console.WriteLine($"Listing: {baseListing.ListingId} - No URL, skipping details");
            return baseListing;
        }

        Console.WriteLine($"Listing: {baseListing.ListingId} - Opening detail page...");
        var detailPageHtml = await _detailScraper.FetchAndParseDetailPageAsync(baseListing.Url);

        if (detailPageHtml == null)
        {
            Console.WriteLine($"Listing: {baseListing.ListingId} - Failed to fetch detail page");
            return baseListing;
        }

        var html = await _detailScraper.GetPageAsync();
        var details = _detailParser.ParseDetailFields(html);
        var metadata = _detailParser.ExtractExposeMetadata(html);
        var originalPrice = _detailParser.ExtractOriginalPrice(html);

        // Expose metadata replaces the card's values, even where the detail page has none
        return baseListing with
        {
            Title = details?.Title ?? baseListing.Title,
            Address = metadata?.Address,
            PostalCode = metadata?.PostalCode,
            District = metadata?.District,
            PriceEur = metadata?.PriceEur,
            OriginalPriceEur = originalPrice ?? metadata?.OriginalPriceEur ?? metadata?.PriceEur,
            RoomsCount = metadata?.RoomsCount,
            LivingAreaSqm = metadata?.LivingAreaSqm,
            CommissionPercent = metadata?.CommissionPercent,
            Details = details,
        };
    }

    protected override async Task CleanupAsync()
    {
        if (_page != null)
        {
            await _page.CloseAsync();
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
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
                    var flat = _extractor.ExtractFlat(card, _selectors, SourceName);
                    if (flat != null)
                    {
                        Console.WriteLine($"Successfully parsed flat: {flat.ListingId}");
                        results.Add(flat);
                    }
                    else
                    {
                        Console.WriteLine("FlatExtractor returned null for this card");
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
