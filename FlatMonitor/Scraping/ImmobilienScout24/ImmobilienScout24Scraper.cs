namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scraper for immobilienscout24.de apartment flats.
/// Uses Playwright for browser automation (the site shows a robot guard to plain HTTP clients), then parses each
/// listing card with the ImmobilienScout24CardParser and each expose page with the ImmobilienScout24DetailParser.
/// </summary>
public sealed class ImmobilienScout24Scraper : FlatScraper
{
    public const string SourceName = "ImmobilienScout24";

    private readonly ImmobilienScout24Options _options;

    public ImmobilienScout24Scraper(StorageOptions storage, ImmobilienScout24Options options, ILogger<ImmobilienScout24Scraper> logger)
        : base(SourceName, HtmlSnapshotStore.ForSource(storage, SourceName, logger), logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task<IScrapeSession> StartAsync(CancellationToken ct)
    {
        var browser = await new ImmobilienScout24Browser(_options, Logger).LaunchAsync(ct);
        return new Session(browser, _options, HtmlSnapshots, Logger);
    }

    /// <summary>One run in one browser, which is on the first listings page when the session starts.</summary>
    private sealed class Session : IScrapeSession
    {
        private readonly ImmobilienScout24BrowserSession _browser;
        private readonly ImmobilienScout24Options _options;
        private readonly ILogger _logger;
        private readonly ImmobilienScout24FlatsPageFetcher _pageFetcher;
        private readonly ImmobilienScout24DetailScraper _detailScraper;
        private readonly ImmobilienScout24CardParser _cardParser;
        private readonly ImmobilienScout24DetailParser _detailParser = new();

        public Session(ImmobilienScout24BrowserSession browser, ImmobilienScout24Options options, HtmlSnapshotStore htmlSnapshots, ILogger logger)
        {
            _browser = browser;
            _options = options;
            _logger = logger;
            _pageFetcher = new ImmobilienScout24FlatsPageFetcher(browser.Page, logger);
            _detailScraper = new ImmobilienScout24DetailScraper(browser.Page, htmlSnapshots, logger);
            _cardParser = new ImmobilienScout24CardParser(logger);
        }

        public async IAsyncEnumerable<List<Flat>> FetchCardsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            // Fetch and parse first page
            _logger.LogDebug("ImmobilienScout24: processing first page");
            var firstPageHtml = await _browser.Page.ContentAsync();

            var firstPageListings = ParseListingsPage(firstPageHtml);
            if (firstPageListings.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No listings on the first page (robot guard?); {await ImmobilienScout24DebugPage.SaveAsync(_options, firstPageHtml, ct)}");
            }

            yield return firstPageListings;

            // Fetch and parse remaining pages; each page is parsed once, and the first one without cards ends it
            await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
            {
                var listings = ParseListingsPage(pageHtml);
                if (listings.Count == 0)
                {
                    _logger.LogInformation("ImmobilienScout24: a page without listings, pagination complete");
                    yield break;
                }

                yield return listings;
            }
        }

        public async Task<Flat> EnrichAsync(Flat card, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(card.Url))
            {
                _logger.LogWarning("ImmobilienScout24: listing {ListingId} has no URL, skipping details", card.ListingId);
                return card;
            }

            _logger.LogDebug("ImmobilienScout24: listing {ListingId}: opening detail page", card.ListingId);
            var html = await _detailScraper.FetchDetailHtmlAsync(card.Url, card.ListingId, ct);
            var detailPage = _detailParser.Parse(html);
            _logger.LogDebug("ImmobilienScout24: listing {ListingId}: parsed details: PropertyType={PropertyType}, BuildYear={BuildYear}, Heating={Heating}",
                card.ListingId, detailPage.Details?.PropertyType, detailPage.Details?.BuildYear, detailPage.Details?.Heating);

            return detailPage.ApplyTo(card);
        }

        public ValueTask DisposeAsync() => _browser.DisposeAsync();

        /// <summary>
        /// Parse all listing cards from a listings page HTML. A card that cannot be read is skipped by the card parser.
        /// </summary>
        private List<Flat> ParseListingsPage(string html)
        {
            var results = new List<Flat>();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Select all listing card containers
            var listingCards = doc.DocumentNode.SelectNodes(ImmobilienScout24Selectors.ListingCardXPath);
            _logger.LogDebug("ImmobilienScout24: page HTML {Bytes} bytes, XPath {XPath} matched {Count} cards",
                html.Length, ImmobilienScout24Selectors.ListingCardXPath, listingCards?.Count ?? 0);

            if (listingCards == null || listingCards.Count == 0)
            {
                _logger.LogWarning("ImmobilienScout24: no listing cards found on page (XPath {XPath})", ImmobilienScout24Selectors.ListingCardXPath);
                return results;
            }

            foreach (var card in listingCards)
            {
                _logger.LogTrace("ImmobilienScout24: card outerHTML (first 200 chars): {Html}", card.OuterHtml[..Math.Min(200, card.OuterHtml.Length)]);
                var flat = _cardParser.Parse(card);
                if (flat != null)
                {
                    _logger.LogDebug("ImmobilienScout24: parsed card {ListingId}", flat.ListingId);
                    results.Add(flat);
                }
                else
                {
                    _logger.LogDebug("ImmobilienScout24: card has no listing ID or cannot be read, skipped");
                }
            }

            _logger.LogDebug("ImmobilienScout24: parsed {Count} listings from page", results.Count);
            return results;
        }
    }
}
