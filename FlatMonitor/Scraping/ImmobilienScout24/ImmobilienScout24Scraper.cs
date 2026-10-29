namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
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

    public ImmobilienScout24Scraper(StorageOptions storage, ILogger<ImmobilienScout24Scraper> logger)
        : base(logger)
    {
        _extractor = new FlatExtractor(logger);
        _selectors = new ImmobilienScout24Selectors();
        _htmlSnapshotsDir = Path.Combine(storage.DataDirectory, "snapshots", "html", SourceName);
    }

    protected override async Task InitializeAsync(CancellationToken ct)
    {
        var browserScraper = new ImmobilienScout24BrowserScraper(Logger);
        var (browser, page) = await browserScraper.LaunchAndAuthenticateAsync();

        if (browser == null || page == null)
        {
            throw new InvalidOperationException("Failed to launch browser or authenticate.");
        }

        _browser = browser;
        _page = page;
        _detailScraper = new ImmobilienScout24DetailScraper(page, _htmlSnapshotsDir, Logger);
        _detailParser = new ImmobilienScout24DetailParser(Logger);
        _pageFetcher = new ImmobilienScout24FlatsPageFetcher(browser, page, Logger);
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
                Logger.LogDebug("Cleared {Count} previous HTML snapshots from {Directory}", files.Length, htmlDir);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not clear previous HTML snapshots");
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
        Logger.LogDebug("ImmobilienScout24: processing first page");
        var firstPageHtml = await _page.ContentAsync();

        // Debug: save raw HTML for inspection if it's suspiciously small
        if (firstPageHtml.Length < 10000)
        {
            var debugPath = Path.Combine(Path.GetTempPath(), $"immobilienscout24_debug_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            await File.WriteAllTextAsync(debugPath, firstPageHtml);
            Logger.LogWarning("ImmobilienScout24: first page HTML is only {Bytes} bytes, saved to {Path}", firstPageHtml.Length, debugPath);
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
            Logger.LogWarning("ImmobilienScout24: listing {ListingId} has no URL, skipping details", baseListing.ListingId);
            return baseListing;
        }

        Logger.LogDebug("ImmobilienScout24: listing {ListingId}: opening detail page", baseListing.ListingId);
        var detailPageHtml = await _detailScraper.FetchAndParseDetailPageAsync(baseListing.Url);

        if (detailPageHtml == null)
        {
            Logger.LogWarning("ImmobilienScout24: listing {ListingId}: failed to fetch detail page", baseListing.ListingId);
            return baseListing;
        }

        var html = await _detailScraper.GetPageAsync();
        var details = _detailParser.ParseDetailFields(html);
        var metadata = _detailParser.ExtractExposeMetadata(html);
        var originalPrice = _detailParser.ExtractOriginalPrice(html);

        // Expose metadata replaces the card's values, even where the detail page has none
        return baseListing with
        {
            Title = _detailParser.ExtractTitle(html) ?? baseListing.Title,
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

            // Select all listing card containers
            var listingCards = doc.DocumentNode.SelectNodes(_selectors.ListingCardXPath);
            Logger.LogDebug("ImmobilienScout24: page HTML {Bytes} bytes, XPath {XPath} matched {Count} cards",
                html.Length, _selectors.ListingCardXPath, listingCards?.Count ?? 0);

            if (listingCards == null || listingCards.Count == 0)
            {
                Logger.LogWarning("ImmobilienScout24: no listing cards found on page, trying alternative selector");
                // Try alternative selector
                listingCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card')]");
                Logger.LogDebug("ImmobilienScout24: alternative selector matched {Count} cards", listingCards?.Count ?? 0);

                if (listingCards == null || listingCards.Count == 0)
                {
                    return results;
                }
            }

            foreach (var card in listingCards)
            {
                try
                {
                    Logger.LogTrace("ImmobilienScout24: card outerHTML (first 200 chars): {Html}", card.OuterHtml[..Math.Min(200, card.OuterHtml.Length)]);
                    var flat = _extractor.ExtractFlat(card, _selectors, SourceName);
                    if (flat != null)
                    {
                        Logger.LogDebug("ImmobilienScout24: parsed card {ListingId}", flat.ListingId);
                        results.Add(flat);
                    }
                    else
                    {
                        Logger.LogDebug("ImmobilienScout24: card has no listing ID, skipped");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "ImmobilienScout24: failed to parse a listing card");
                }
            }

            Logger.LogDebug("ImmobilienScout24: parsed {Count} listings from page", results.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ImmobilienScout24: error parsing listings page");
        }

        return results;
    }
}
