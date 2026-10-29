namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scraper for immobilienscout24.de apartment flats.
/// Uses Playwright for browser automation (Google OAuth, robot guard), then parses each listing card with the ImmobilienScout24CardParser.
/// Processes each flat's details immediately as pages are fetched.
/// Saves flats to database in batches to avoid memory bloat on large datasets.
/// </summary>
public sealed class ImmobilienScout24Scraper : FlatScraper
{
    public const string SourceName = "ImmobilienScout24";

    private readonly ImmobilienScout24CardParser _cardParser;
    private readonly ImmobilienScout24Selectors _selectors;
    private readonly HtmlSnapshotStore _htmlSnapshots;
    private readonly ImmobilienScout24Options _options;
    private readonly ImmobilienScout24DetailParser _detailParser;
    private ImmobilienScout24BrowserSession? _session;
    private ImmobilienScout24DetailScraper? _detailScraper;
    private ImmobilienScout24FlatsPageFetcher? _pageFetcher;

    public override string Source => SourceName;

    public ImmobilienScout24Scraper(StorageOptions storage, ImmobilienScout24Options options, ILogger<ImmobilienScout24Scraper> logger)
        : base(logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cardParser = new ImmobilienScout24CardParser(logger);
        _selectors = new ImmobilienScout24Selectors();
        _htmlSnapshots = HtmlSnapshotStore.ForSource(storage, SourceName, logger);
        _detailParser = new ImmobilienScout24DetailParser();
    }

    protected override async Task InitializeAsync(CancellationToken ct)
    {
        _session = await new ImmobilienScout24BrowserScraper(_options, Logger).LaunchAndAuthenticateAsync();
        _detailScraper = new ImmobilienScout24DetailScraper(_session.Page, _htmlSnapshots, Logger);
        _pageFetcher = new ImmobilienScout24FlatsPageFetcher(_session.Page, Logger);
    }

    protected override Task ClearPreviousSnapshotsAsync(CancellationToken ct) => _htmlSnapshots.ClearAsync(ct);

    protected override async IAsyncEnumerable<List<Flat>> FetchAndParseAllPagesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        if (_session == null || _pageFetcher == null)
        {
            yield break;
        }

        // Fetch and parse first page
        Logger.LogDebug("ImmobilienScout24: processing first page");
        var firstPageHtml = await _session.Page.ContentAsync();

        // A suspiciously small page is usually the robot guard, not the listings
        if (firstPageHtml.Length < 10000)
        {
            await WarnAboutSmallFirstPageAsync(firstPageHtml, ct);
        }

        var firstPageListings = ParseListingsPage(firstPageHtml);
        if (firstPageListings.Count == 0)
        {
            throw new InvalidOperationException("No listings on the first page (robot guard?)");
        }

        yield return firstPageListings;

        // Fetch and parse remaining pages
        await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
        {
            var listings = ParseListingsPage(pageHtml);
            yield return listings;
        }
    }

    private async Task WarnAboutSmallFirstPageAsync(string html, CancellationToken ct)
    {
        if (!_options.SaveDebugPages)
        {
            Logger.LogWarning("ImmobilienScout24: first page HTML is only {Bytes} bytes (set {Section}:SaveDebugPages to true to save it)",
                html.Length, ImmobilienScout24Options.SectionName);
            return;
        }

        var debugPath = Path.Combine(Path.GetTempPath(), $"immobilienscout24_debug_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        await File.WriteAllTextAsync(debugPath, html, ct);
        Logger.LogWarning("ImmobilienScout24: first page HTML is only {Bytes} bytes, saved to {Path}", html.Length, debugPath);
    }

    protected override async Task<Flat> EnrichListingWithDetailsAsync(Flat baseListing, CancellationToken ct)
    {
        if (_detailScraper == null)
        {
            return baseListing;
        }

        if (string.IsNullOrEmpty(baseListing.Url))
        {
            Logger.LogWarning("ImmobilienScout24: listing {ListingId} has no URL, skipping details", baseListing.ListingId);
            return baseListing;
        }

        Logger.LogDebug("ImmobilienScout24: listing {ListingId}: opening detail page", baseListing.ListingId);
        var html = await _detailScraper.FetchDetailHtmlAsync(baseListing.Url, baseListing.ListingId, ct);
        var detailPage = _detailParser.Parse(html);
        Logger.LogDebug("ImmobilienScout24: listing {ListingId}: parsed details: PropertyType={PropertyType}, BuildYear={BuildYear}, Heating={Heating}",
            baseListing.ListingId, detailPage.Details?.PropertyType, detailPage.Details?.BuildYear, detailPage.Details?.Heating);

        return detailPage.ApplyTo(baseListing);
    }

    protected override async Task CleanupAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }

    /// <summary>
    /// Parse all listing cards from a listings page HTML. A card that cannot be read is skipped by the card parser.
    /// </summary>
    private List<Flat> ParseListingsPage(string html)
    {
        var results = new List<Flat>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Select all listing card containers
        var listingCards = doc.DocumentNode.SelectNodes(_selectors.ListingCardXPath);
        Logger.LogDebug("ImmobilienScout24: page HTML {Bytes} bytes, XPath {XPath} matched {Count} cards",
            html.Length, _selectors.ListingCardXPath, listingCards?.Count ?? 0);

        if (listingCards == null || listingCards.Count == 0)
        {
            Logger.LogWarning("ImmobilienScout24: no listing cards found on page (XPath {XPath})", _selectors.ListingCardXPath);
            return results;
        }

        foreach (var card in listingCards)
        {
            Logger.LogTrace("ImmobilienScout24: card outerHTML (first 200 chars): {Html}", card.OuterHtml[..Math.Min(200, card.OuterHtml.Length)]);
            var flat = _cardParser.Parse(card);
            if (flat != null)
            {
                Logger.LogDebug("ImmobilienScout24: parsed card {ListingId}", flat.ListingId);
                results.Add(flat);
            }
            else
            {
                Logger.LogDebug("ImmobilienScout24: card has no listing ID or cannot be read, skipped");
            }
        }

        Logger.LogDebug("ImmobilienScout24: parsed {Count} listings from page", results.Count);
        return results;
    }
}
