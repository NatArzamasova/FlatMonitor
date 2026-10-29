namespace FlatMonitor.Scraping.OhneMakler;

using System.Runtime.CompilerServices;
using HtmlAgilityPack;
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

    // Yields raw HTML for each results page, starting at page 1, stopping
    // as soon as a fetched page contains zero listing-card links or a hard safety cap is hit.
    // A failing request throws on any page: stopping early would look like a successful scrape with fewer flats.
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
            string html = await response.Content.ReadAsStringAsync(ct);

            // Parse the HTML to count flat-card links; stop if this page has zero.
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var listingLinks = doc.DocumentNode.SelectNodes(OhneMaklerSelectors.ListingLinkXPath);
            int listingCount = listingLinks?.Count ?? 0;

            if (listingCount == 0)
            {
                // The first page always has listings; none means changed markup or a blocked request
                if (pageNumber == 1)
                    throw new InvalidOperationException($"No listings on the first page ({url})");

                _logger.LogInformation("OhneMakler: page {Page} has no listings, pagination complete", pageNumber);
                yield break;
            }

            _logger.LogInformation("OhneMakler: page {Page}: {Count} listings found", pageNumber, listingCount);
            yield return html;
            pageNumber++;
        }

        // Hard cap reached.
        _logger.LogWarning("OhneMakler: pagination reached the safety cap of {MaxPages} pages, stopping", MaxPagesLimit);
    }
}
