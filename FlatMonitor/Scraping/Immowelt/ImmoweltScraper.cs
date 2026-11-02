namespace FlatMonitor.Scraping.Immowelt;

using System.Runtime.CompilerServices;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scraper for immowelt.de flats. Works in a tab of the user's Chrome (<see cref="ImmoweltBrowser"/>): DataDome lets
/// neither <c>HttpClient</c> nor Playwright's own browser through. Reads the results pages' data scripts with the
/// <see cref="ImmoweltListParser"/>.
/// </summary>
public sealed class ImmoweltScraper : FlatScraper
{
    private readonly ImmoweltOptions _options;

    public ImmoweltScraper(StorageOptions storage, ImmoweltOptions options, ILogger<ImmoweltScraper> logger)
        : base(ImmoweltPages.SourceName, HtmlPageArchive.ForSource(storage, ImmoweltPages.SourceName, logger), logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task<IScrapeSession> StartAsync(CancellationToken ct)
    {
        var browser = await new ImmoweltBrowser(_options, Logger).AttachAsync(ct);
        return new Session(browser, _options, Logger);
    }

    /// <summary>One run in one tab of the user's Chrome.</summary>
    private sealed class Session : IScrapeSession
    {
        private readonly ImmoweltBrowserSession _browser;
        private readonly ImmoweltFlatsPageFetcher _pageFetcher;

        public Session(ImmoweltBrowserSession browser, ImmoweltOptions options, ILogger logger)
        {
            _browser = browser;
            _pageFetcher = new ImmoweltFlatsPageFetcher(browser.Page, options, logger);
        }

        public async IAsyncEnumerable<List<Flat>> FetchCardsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var page in _pageFetcher.FetchAllPagesAsync(ct))
                yield return page.Flats.ToList();
        }

        // Step 7 of immowelt.md: open the expose and apply the ImmoweltDetailParser's page. Until then: the card
        public Task<Flat?> EnrichAsync(Flat card, CancellationToken ct) => Task.FromResult<Flat?>(card);

        public ValueTask DisposeAsync() => _browser.DisposeAsync();
    }
}
