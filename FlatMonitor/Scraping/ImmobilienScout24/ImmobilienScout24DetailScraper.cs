namespace FlatMonitor.Scraping.ImmobilienScout24;

using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Fetches detail pages from ImmobilienScout24 expose URLs.
/// Uses the authenticated browser session to access detail pages.
/// Saves each detail page's HTML through the <see cref="HtmlSnapshotStore"/> before returning it, so a page that
/// fails to parse is kept.
/// </summary>
public class ImmobilienScout24DetailScraper
{
    /// <summary>Any of these means the expose's attribute list has rendered.</summary>
    public const string ExposeContentSelector = "dd[class*='is24qa-']";
    public const int ExposeContentTimeoutMs = 5000;

    private readonly IPage _page;
    private readonly HtmlSnapshotStore _htmlSnapshots;
    private readonly ILogger _logger;

    public ImmobilienScout24DetailScraper(IPage page, HtmlSnapshotStore htmlSnapshots, ILogger logger)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _htmlSnapshots = htmlSnapshots ?? throw new ArgumentNullException(nameof(htmlSnapshots));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Opens the expose URL and returns the page's HTML. Throws if the page cannot be loaded;
    /// <see cref="Common.FlatScraper"/> then keeps the card's data.
    /// </summary>
    public async Task<string> FetchDetailHtmlAsync(string url, string listingId, CancellationToken ct = default)
    {
        _logger.LogDebug("ImmobilienScout24: fetching detail page {Url}", url);
        await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
        await WaitForExposeContentAsync(listingId);

        var html = await _page.ContentAsync();
        _logger.LogDebug("ImmobilienScout24: detail page fetched ({Bytes} bytes)", html.Length);

        await _htmlSnapshots.SaveAsync(listingId, html, ct);
        return html;
    }

    /// <summary>Gives the page time to render; a page without the expected content is still returned.</summary>
    private async Task WaitForExposeContentAsync(string listingId)
    {
        try
        {
            await _page.WaitForSelectorAsync(ExposeContentSelector,
                new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached, Timeout = ExposeContentTimeoutMs });
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("ImmobilienScout24: listing {ListingId}: no expose attributes after {Timeout} ms, reading the page as it is",
                listingId, ExposeContentTimeoutMs);
        }
    }
}
