namespace FlatMonitor.Scraping.OhneMakler;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scraper for ohne-makler.net flats.
/// Uses HttpClient for fetching the results pages and each listing's detail page.
/// </summary>
public sealed class OhneMaklerScraper : FlatScraper
{
    public const string SourceName = "OhneMakler";

    private readonly HttpClient _httpClient;

    public OhneMaklerScraper(HttpClient httpClient, StorageOptions storage, ILogger<OhneMaklerScraper> logger)
        : base(SourceName, HtmlPageArchive.ForSource(storage, SourceName, logger), logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    // Nothing to open: the HttpClient is a DI singleton, managed by the container
    protected override Task<IScrapeSession> StartAsync(CancellationToken ct) =>
        Task.FromResult<IScrapeSession>(new Session(_httpClient, HtmlPages, Logger));

    private sealed class Session : IScrapeSession
    {
        private readonly HttpClient _httpClient;
        private readonly HtmlPageArchive _htmlPages;
        private readonly ILogger _logger;
        private readonly OhneMaklerFlatsPageFetcher _pageFetcher;
        private readonly OhneMaklerFlatCardParser _cardParser;
        private readonly OhneMaklerDetailPageParser _detailParser = new();

        public Session(HttpClient httpClient, HtmlPageArchive htmlPages, ILogger logger)
        {
            _httpClient = httpClient;
            _htmlPages = htmlPages;
            _logger = logger;
            _pageFetcher = new OhneMaklerFlatsPageFetcher(httpClient, logger);
            _cardParser = new OhneMaklerFlatCardParser(logger);
        }

        public async IAsyncEnumerable<List<Flat>> FetchCardsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            int pageNumber = 0;

            // Iterate through the results pages; each page is parsed once, and the first one without cards ends it
            await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
            {
                pageNumber++;
                var cards = _cardParser.ParsePage(pageHtml);
                if (cards.Count == 0)
                {
                    // The first page always has listings; none means changed markup or a blocked request
                    if (pageNumber == 1)
                        throw new InvalidOperationException($"No listings on the first page ({OhneMaklerSelectors.PageUrl(1)})");

                    _logger.LogInformation("OhneMakler: page {Page} has no listings, pagination complete", pageNumber);
                    yield break;
                }

                var pageListings = OhneMaklerKarlsruheResults.TakeUntilOutsideKarlsruhe(cards, out var firstOutside);
                yield return pageListings;

                if (firstOutside != null)
                {
                    _logger.LogInformation(
                        "OhneMakler: stopping pagination, listing outside Karlsruhe postcodes ({PostalCode}) on page {Page}",
                        firstOutside.PostalCode, pageNumber);
                    yield break;
                }
            }
        }

        public async Task<Flat> EnrichAsync(Flat card, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(card.Url))
            {
                return card;
            }

            // The detail page is the source of truth; card data fills the gaps.
            // A failing page throws: FlatScraper logs it and keeps the card's data.
            var html = await FetchDetailPageAsync(card.Url, card.ListingId, ct);
            var page = _detailParser.Parse(html);

            return card with
            {
                Title = page.Title ?? card.Title,
                Address = page.Address,
                PriceEur = page.Price ?? card.PriceEur,
                OriginalPriceEur = page.Price ?? card.PriceEur,
                RoomsCount = page.Rooms ?? card.RoomsCount,
                LivingAreaSqm = page.Area ?? card.LivingAreaSqm,
                Details = page.Details,
                // ohne-makler.net lists flats without an estate agent, so there is no commission
                CommissionPercent = 0,
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task<string> FetchDetailPageAsync(string url, string listingId, CancellationToken ct)
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(ct);

            // Awaited: a write still running could otherwise race with the next scrape's ClearAsync
            await _htmlPages.SaveAsync(listingId, html, ct);

            return html;
        }
    }
}
