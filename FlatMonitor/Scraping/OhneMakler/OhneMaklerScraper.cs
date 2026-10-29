namespace FlatMonitor.Scraping.OhneMakler;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;


/// <summary>
/// Scraper for ohne-makler.net flats.
/// Uses HttpClient for fetching pages, processes each listing's details immediately.
/// Saves flats to database in batches to avoid memory bloat on large datasets.
/// </summary>
public sealed class OhneMaklerScraper : FlatScraper
{
    public const string SourceName = "OhneMakler";

    private readonly HttpClient _httpClient;
    private readonly HtmlSnapshotStore _htmlSnapshots;
    private OhneMaklerFlatsPageFetcher? _pageFetcher;
    private readonly OhneMaklerFlatCardParser _cardParser;
    private readonly OhneMaklerDetailPageParser _detailParser;

    public override string Source => SourceName;

    public OhneMaklerScraper(HttpClient httpClient, StorageOptions storage, ILogger<OhneMaklerScraper> logger)
        : base(logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _htmlSnapshots = HtmlSnapshotStore.ForSource(storage, SourceName, logger);
        _cardParser = new OhneMaklerFlatCardParser(logger);
        _detailParser = new OhneMaklerDetailPageParser();
    }

    protected override Task InitializeAsync(CancellationToken ct)
    {
        _pageFetcher = new OhneMaklerFlatsPageFetcher(_httpClient, Logger);
        return Task.CompletedTask;
    }

    protected override Task ClearPreviousSnapshotsAsync(CancellationToken ct) => _htmlSnapshots.ClearAsync(ct);

    protected override async IAsyncEnumerable<List<Flat>> FetchAndParseAllPagesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        if (_pageFetcher == null)
        {
            yield break;
        }

        int pageNumber = 0;

        // Iterate through all results pages.
        await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
        {
            pageNumber++;
            var cards = _cardParser.ParsePage(pageHtml).Select(c => c.Flat);
            var pageListings = OhneMaklerKarlsruheResults.TakeUntilOutsideKarlsruhe(cards, out var firstOutside);
            yield return pageListings;

            if (firstOutside != null)
            {
                Logger.LogInformation(
                    "OhneMakler: stopping pagination, listing outside Karlsruhe postcodes ({PostalCode}) on page {Page}",
                    firstOutside.PostalCode, pageNumber);
                yield break;
            }
        }
    }

    protected override async Task<Flat> EnrichListingWithDetailsAsync(Flat baseListing, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(baseListing.DetailPageUrl))
        {
            return baseListing;
        }

        // The detail page is the source of truth; card data fills the gaps.
        // A failing page throws: FlatScraper logs it and keeps the card's data.
        var html = await FetchDetailPageAsync(baseListing.DetailPageUrl, baseListing.ListingId, ct);
        var page = _detailParser.Parse(html);

        return baseListing with
        {
            Title = page.Title ?? baseListing.Title,
            Address = page.Address,
            PriceEur = page.Price ?? baseListing.PriceEur,
            OriginalPriceEur = page.Price ?? baseListing.PriceEur,
            RoomsCount = page.Rooms ?? baseListing.RoomsCount,
            LivingAreaSqm = page.Area ?? baseListing.LivingAreaSqm,
            Details = page.Details,
            // ohne-makler.net lists flats without an estate agent, so there is no commission
            CommissionPercent = 0,
        };
    }

    protected override async Task CleanupAsync()
    {
        // HttpClient is a DI singleton, managed by container — do not dispose
    }

    private async Task<string> FetchDetailPageAsync(string url, string listingId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);

        // Awaited: a write still running could otherwise race with the next scrape's ClearAsync
        await _htmlSnapshots.SaveAsync(listingId, html, ct);

        return html;
    }
}
