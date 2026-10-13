namespace workInProgress.Scraping.OhneMakler;

using System.Runtime.CompilerServices;
using HtmlAgilityPack;

internal sealed class OhneMaklerListingsPageFetcher
{
    private readonly HttpClient _httpClient;
    private const int MaxPagesLimit = 50;

    public OhneMaklerListingsPageFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    // Yields raw HTML for each results page, starting at page 1, stopping
    // as soon as a fetched page contains zero listing-card links or a hard safety cap is hit.
    public async IAsyncEnumerable<string> FetchAllPagesHtmlAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int pageNumber = 1;

        while (pageNumber <= MaxPagesLimit)
        {
            string url = OhneMaklerSelectors.PageUrl(pageNumber);
            Console.WriteLine($"Fetching page {pageNumber}...");

            string html;
            try
            {
                var response = await _httpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                html = await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex) when (pageNumber == 1)
            {
                // Page 1 is fatal — no listing index, can't proceed.
                throw new InvalidOperationException(
                    $"Failed to fetch the first page from {url}. Scrape cannot continue.", ex);
            }
            catch (Exception ex)
            {
                // Later pages: log warning and stop pagination.
                Console.WriteLine(
                    $"Warning: Failed to fetch page {pageNumber} (will stop pagination): {ex.Message}");
                yield break;
            }

            // Parse the HTML to count listing-card links; stop if this page has zero.
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var listingLinks = doc.DocumentNode.SelectNodes(OhneMaklerSelectors.ListingLinkXPath);
            int listingCount = listingLinks?.Count ?? 0;

            if (listingCount == 0)
            {
                Console.WriteLine($"Page {pageNumber} returned zero listings. Pagination complete.");
                yield break;
            }

            Console.WriteLine($"Page {pageNumber}: {listingCount} listings found.");
            yield return html;
            pageNumber++;
        }

        // Hard cap reached.
        Console.WriteLine($"Warning: Pagination reached safety cap of {MaxPagesLimit} pages. Stopping.");
    }
}
