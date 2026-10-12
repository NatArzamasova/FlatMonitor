namespace workInProgress.Scraping;

using workInProgress.Models;
using workInProgress.Storage;

public sealed class ListingScraper
{
    private readonly HttpClient _httpClient;
    private readonly ListingsPageFetcher _pageFetcher;
    private readonly ListingCardParser _cardParser;
    private readonly DetailPageParser _detailParser;
    private readonly SnapshotWriter _snapshotWriter;

    public ListingScraper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _pageFetcher = new ListingsPageFetcher(_httpClient);
        _cardParser = new ListingCardParser();
        _detailParser = new DetailPageParser();
        _snapshotWriter = new SnapshotWriter();
    }

    public async Task RunAsync()
    {
        var listings = await ScrapeAllListingsAsync();

        if (listings.Count == 0)
        {
            Console.WriteLine("No listings found.");
            return;
        }

        var snapshotPath = await _snapshotWriter.WriteSnapshotAsync(listings);
        Console.WriteLine($"Snapshot written: {snapshotPath}");
        Console.WriteLine($"Total listings: {listings.Count}");
    }

    public async Task<List<Listing>> ScrapeAllListingsAsync(CancellationToken ct = default)
    {
        var allListings = new List<Listing>();
        var pageNumber = 0;
        var shouldStop = false;

        // Iterate through all results pages.
        await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
        {
            pageNumber++;

            // Parse listing cards from this page.
            var pageListings = _cardParser.ParsePage(pageHtml);

            foreach (var (listing, detailPageUrl) in pageListings)
            {
                // Check if postcode is in Karlsruhe whitelist
                if (!string.IsNullOrEmpty(listing.PostalCode) &&
                    !Selectors.KarlsruhePostcodes.Contains(listing.PostalCode))
                {
                    Console.WriteLine(
                        $"Stopping pagination: Found listing outside Karlsruhe postcodes (postcode: {listing.PostalCode}) on page {pageNumber}");
                    shouldStop = true;
                    break;
                }

                try
                {
                    // Fetch and parse the detail page.
                    string detailHtml = await FetchDetailPageAsync(detailPageUrl, ct);
                    var details = _detailParser.ParseDetailPage(detailHtml);

                    // Extract the address from the detail page HTML.
                    string? address = ExtractAddressFromDetailPage(detailHtml);

                    // Update the listing with detail-page data.
                    var enrichedListing = new Listing
                    {
                        ListingId = listing.ListingId,
                        Url = listing.Url,
                        Title = listing.Title,
                        Address = address,
                        PostalCode = listing.PostalCode,
                        District = listing.District,
                        PriceEur = listing.PriceEur,
                        RoomsCount = listing.RoomsCount,
                        LivingAreaSqm = listing.LivingAreaSqm,
                        Details = details,
                        ScrapedAtUtc = listing.ScrapedAtUtc,
                    };

                    allListings.Add(enrichedListing);
                }
                catch (Exception ex)
                {
                    // Per confirmed behavior: log warning, still include listing with partial data.
                    Console.WriteLine(
                        $"Warning: Failed to fetch/parse detail page for listing {listing.ListingId} ({listing.Url}): {ex.Message}");
                    allListings.Add(listing);
                }
            }

            if (shouldStop)
                break;
        }

        return allListings;
    }

    private async Task<string> FetchDetailPageAsync(string relativeUrl, CancellationToken ct)
    {
        var absoluteUrl = relativeUrl.StartsWith("http")
            ? relativeUrl
            : new Uri(new Uri(Selectors.ListingsBaseUrl), relativeUrl).AbsoluteUri;

        var response = await _httpClient.GetAsync(absoluteUrl, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string? ExtractAddressFromDetailPage(string html)
    {
        try
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Address is typically in a prominent location on the detail page, often in a heading or address field.
            // Look for common patterns: <h1>, <address>, or text near "Adresse".
            var addressNode = doc.DocumentNode.SelectSingleNode("//h1 | //address");
            if (addressNode != null)
            {
                var text = addressNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            // Fallback: search for "Adresse" label and extract following text
            var adresseLabel = doc.DocumentNode.SelectSingleNode(
                "//text()[contains(., 'Adresse')]/parent::*/following-sibling::*[1]");
            if (adresseLabel != null)
            {
                var text = adresseLabel.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
