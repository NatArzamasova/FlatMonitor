namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Fetches flats pages from immobilienscout24.de using Playwright browser automation.
/// Handles pagination by detecting and clicking the next-page button until no more pages exist.
/// The page and browser belong to the scraper's <see cref="ImmobilienScout24BrowserSession"/>.
/// </summary>
internal sealed class ImmobilienScout24FlatsPageFetcher
{
    private const int MaxPagesLimit = 50;
    private const string ListingCardSelector = "div[class*='listing-card'][data-obid]";

    // True once the first listing card is no longer the one shown before clicking "next"
    private const string FirstCardChangedScript = """
        ([selector, previousId]) => {
            const card = document.querySelector(selector);
            return card !== null && card.getAttribute('data-obid') !== previousId;
        }
        """;

    private readonly IPage _page;
    private readonly ILogger _logger;

    public ImmobilienScout24FlatsPageFetcher(IPage page, ILogger logger)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Yields raw HTML for all pages, starting from page 2 onwards.
    /// Assumes the browser is already on page 1; fetches pages 2, 3, etc.
    /// Stops when the next-page button is disabled, a page has no listings, or after reaching the safety limit.
    /// Any other failure throws: stopping early would look like a successful scrape with fewer flats.
    /// </summary>
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int pageNumber = 2;

        while (pageNumber <= MaxPagesLimit)
        {
            _logger.LogDebug("ImmobilienScout24: fetching page {Page}", pageNumber);

            var pageResult = await FetchPageInternalAsync(pageNumber, ct);

            if (pageResult == null)
            {
                yield break;
            }

            yield return pageResult;
            pageNumber++;
        }

        // Hard cap reached
        _logger.LogWarning("ImmobilienScout24: pagination reached the safety cap of {MaxPages} pages, stopping", MaxPagesLimit);
    }

    private async Task<string?> FetchPageInternalAsync(int pageNumber, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Click next page button to navigate to the target page
        _logger.LogDebug("ImmobilienScout24: page {Page}: clicking next page button", pageNumber);
        var nextButton = await _page.QuerySelectorAsync("button[data-testid='pagination-button-next']:not([disabled])");
        if (nextButton == null)
        {
            _logger.LogInformation("ImmobilienScout24: next page button not found or disabled, pagination complete");
            return null;
        }

        var previousFirstCard = await _page.QuerySelectorAsync(ListingCardSelector);
        var previousFirstId = previousFirstCard == null ? null : await previousFirstCard.GetAttributeAsync("data-obid");

        await nextButton.ClickAsync();

        // Wait until the next page's listings have rendered
        _logger.LogDebug("ImmobilienScout24: page {Page}: waiting for page load", pageNumber);
        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 60000 });
        await _page.WaitForFunctionAsync(FirstCardChangedScript, new object?[] { ListingCardSelector, previousFirstId },
            new PageWaitForFunctionOptions { Timeout = 60000 });
        _logger.LogDebug("ImmobilienScout24: page {Page}: loaded, extracting HTML", pageNumber);

        // Extract current page HTML (now on the new page)
        var html = await _page.ContentAsync();
        _logger.LogDebug("ImmobilienScout24: page {Page}: HTML extracted ({Bytes} bytes)", pageNumber, html.Length);

        // Count listing cards on this page
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var listingCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'listing-card') and @data-obid]");
        int listingCount = listingCards?.Count ?? 0;
        if (listingCount == 0)
        {
            _logger.LogInformation("ImmobilienScout24: page {Page} has no listings, pagination complete", pageNumber);
            return null;
        }

        _logger.LogInformation("ImmobilienScout24: page {Page}: {Count} listings found", pageNumber, listingCount);
        return html;
    }
}
