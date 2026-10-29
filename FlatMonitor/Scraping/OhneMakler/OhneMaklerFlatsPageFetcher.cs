namespace FlatMonitor.Scraping.OhneMakler;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

internal sealed class OhneMaklerFlatsPageFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const int MaxPagesLimit = 50;

    public OhneMaklerFlatsPageFetcher(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    // Yields raw HTML for each results page, starting at page 1, until the caller stops reading (the session stops
    // at the first page without listings) or a hard safety cap is hit. A page is only fetched when the caller asks
    // for it. A failing request throws on any page: stopping early would look like a successful scrape with fewer flats.
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int pageNumber = 1;

        while (pageNumber <= MaxPagesLimit)
        {
            string url = OhneMaklerSelectors.PageUrl(pageNumber);
            _logger.LogDebug("OhneMakler: fetching page {Page}", pageNumber);

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            yield return await response.Content.ReadAsStringAsync(ct);
            pageNumber++;
        }

        // Hard cap reached.
        _logger.LogWarning("OhneMakler: pagination reached the safety cap of {MaxPages} pages, stopping", MaxPagesLimit);
    }
}
