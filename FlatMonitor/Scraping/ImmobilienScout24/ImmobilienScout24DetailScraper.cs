namespace FlatMonitor.Scraping.ImmobilienScout24;

using FlatMonitor.Models;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Fetches and parses detail pages from ImmobilienScout24 expose URLs.
/// Uses the authenticated browser session to access detail pages.
/// Saves each detail page's HTML through the <see cref="HtmlSnapshotStore"/>, before parsing it.
/// </summary>
public class ImmobilienScout24DetailScraper
{
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
    /// Fetch and parse detail page from the given expose URL.
    /// Saves the HTML first, so it is kept even when parsing fails.
    /// </summary>
    public async Task<FlatDetails?> FetchAndParseDetailPageAsync(string url, string listingId)
    {
        try
        {
            _logger.LogDebug("ImmobilienScout24: fetching detail page {Url}", url);
            await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await Task.Delay(1000); // Allow time for content to render

            var html = await _page.ContentAsync();
            _logger.LogDebug("ImmobilienScout24: detail page fetched ({Bytes} bytes)", html.Length);

            await _htmlSnapshots.SaveAsync(listingId, html);

            // Parse detail fields
            var parser = new ImmobilienScout24DetailParser(_logger);
            var details = parser.ParseDetailFields(html);

            if (details != null)
            {
                _logger.LogDebug("ImmobilienScout24: parsed details: PropertyType={PropertyType}, BuildYear={BuildYear}, Heating={Heating}",
                    details.PropertyType, details.BuildYear, details.Heating);
            }

            return details;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImmobilienScout24: error fetching detail page {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Extract address from the currently loaded page.
    /// Must be called after FetchAndParseDetailPageAsync.
    /// </summary>
    public async Task<string?> ExtractAddressFromCurrentPageAsync()
    {
        try
        {
            var html = await _page.ContentAsync();
            var parser = new ImmobilienScout24DetailParser(_logger);
            return parser.ExtractAddress(html);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImmobilienScout24: error extracting address");
            return null;
        }
    }

    /// <summary>
    /// Get the HTML of the currently loaded page.
    /// Used after FetchAndParseDetailPageAsync to extract additional metadata.
    /// </summary>
    public async Task<string> GetPageAsync()
    {
        return await _page.ContentAsync();
    }
}
