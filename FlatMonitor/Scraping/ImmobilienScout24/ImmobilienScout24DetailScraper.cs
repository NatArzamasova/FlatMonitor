namespace FlatMonitor.Scraping.ImmobilienScout24;

using FlatMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

/// <summary>
/// Fetches and parses detail pages from ImmobilienScout24 expose URLs.
/// Uses the authenticated browser session to access detail pages.
/// Saves detail page HTML to /data/snapshots/html/ImmobilienScout24/ for archiving.
/// </summary>
public class ImmobilienScout24DetailScraper
{
    private readonly IPage _page;
    private readonly string _htmlSnapshotsDir;
    private readonly ILogger _logger;

    public ImmobilienScout24DetailScraper(IPage page, string htmlSnapshotsDir, ILogger logger)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _htmlSnapshotsDir = htmlSnapshotsDir ?? throw new ArgumentNullException(nameof(htmlSnapshotsDir));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Directory.CreateDirectory(_htmlSnapshotsDir);
    }

    /// <summary>
    /// Fetch and parse detail page from the given expose URL.
    /// Saves the HTML to /data/snapshots/html/ImmobilienScout24/ for archiving.
    /// </summary>
    public async Task<FlatDetails?> FetchAndParseDetailPageAsync(string url)
    {
        try
        {
            _logger.LogDebug("ImmobilienScout24: fetching detail page {Url}", url);
            await _page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await Task.Delay(1000); // Allow time for content to render

            var html = await _page.ContentAsync();
            _logger.LogDebug("ImmobilienScout24: detail page fetched ({Bytes} bytes)", html.Length);

            // Extract listing ID from URL for filename
            var listingId = ExtractListingIdFromUrl(url);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{listingId}_{timestamp}.html";
            var filePath = Path.Combine(_htmlSnapshotsDir, fileName);

            await File.WriteAllTextAsync(filePath, html);
            _logger.LogDebug("ImmobilienScout24: detail page saved to {Path}", filePath);

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

    private static string ExtractListingIdFromUrl(string url)
    {
        // Extract listing ID from URL like: https://www.immobilienscout24.de/expose/12345678
        var parts = url.Split('/');
        var exposeIndex = Array.IndexOf(parts, "expose");
        if (exposeIndex >= 0 && exposeIndex < parts.Length - 1)
        {
            return parts[exposeIndex + 1];
        }
        // Fallback to hash of URL if ID cannot be extracted
        return Math.Abs(url.GetHashCode()).ToString();
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
