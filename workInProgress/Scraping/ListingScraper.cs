namespace workInProgress.Scraping;

using System.Text.RegularExpressions;
using workInProgress.Models;
using workInProgress.Storage;

public sealed class ListingScraper : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ListingsPageFetcher _pageFetcher;
    private readonly ListingCardParser _cardParser;
    private readonly DetailPageParser _detailParser;
    private readonly SnapshotWriter _snapshotWriter;
    private readonly ListingsRepository _repository;

    public ListingScraper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _pageFetcher = new ListingsPageFetcher(_httpClient);
        _cardParser = new ListingCardParser();
        _detailParser = new DetailPageParser();
        _snapshotWriter = new SnapshotWriter();
        _repository = new ListingsRepository();
    }

    public async Task RunAsync()
    {
        var listings = await ScrapeAllListingsAsync();

        if (listings.Count == 0)
        {
            Console.WriteLine("No listings found.");
            return;
        }

        await _repository.InitializeAsync();
        await _repository.UpsertListingsAsync(listings);

        var snapshotPath = await _snapshotWriter.WriteSnapshotAsync(listings);
        Console.WriteLine($"Snapshot written: {snapshotPath}");
        Console.WriteLine($"Database updated with {listings.Count} listings");
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

                    // Extract title and address from the detail page HTML.
                    string? title = ExtractTitleFromDetailPage(detailHtml);
                    string? address = ExtractAddressFromDetailPage(detailHtml);

                    // Update the listing with detail-page data.
                    var absoluteUrl = listing.Url.StartsWith("http")
                        ? listing.Url
                        : new Uri(new Uri(Selectors.ListingsBaseUrl), listing.Url).AbsoluteUri;

                    var enrichedListing = new Listing
                    {
                        ListingId = listing.ListingId,
                        Url = absoluteUrl,
                        Title = title ?? listing.Title,
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
                    var absoluteUrl = listing.Url.StartsWith("http")
                        ? listing.Url
                        : new Uri(new Uri(Selectors.ListingsBaseUrl), listing.Url).AbsoluteUri;

                    var partialListing = new Listing
                    {
                        ListingId = listing.ListingId,
                        Url = absoluteUrl,
                        Title = listing.Title,
                        Address = null,
                        PostalCode = listing.PostalCode,
                        District = listing.District,
                        PriceEur = listing.PriceEur,
                        RoomsCount = listing.RoomsCount,
                        LivingAreaSqm = listing.LivingAreaSqm,
                        Details = listing.Details,
                        ScrapedAtUtc = listing.ScrapedAtUtc,
                    };

                    Console.WriteLine(
                        $"Warning: Failed to fetch/parse detail page for listing {listing.ListingId} ({absoluteUrl}): {ex.Message}");
                    allListings.Add(partialListing);
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

    private static string? ExtractTitleFromDetailPage(string html)
    {
        try
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Extract h1 heading - this is the property title/description
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
            if (titleNode != null)
            {
                var text = titleNode.InnerText?.Trim();
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

    private static string? ExtractAddressFromDetailPage(string html)
    {
        try
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Address appears between <h1> (title) and first <h2> (section header).
            // Look for text/element immediately after h1, typically an anchor or div.
            var h1 = doc.DocumentNode.SelectSingleNode("//h1");
            if (h1 != null)
            {
                // Find the next sibling that contains address-like text (e.g., street address, postal code, city)
                var node = h1.NextSibling;
                while (node != null)
                {
                    // Stop at h2 or other major section headers
                    if (node.Name == "h2" || node.Name == "h3")
                        break;

                    if (node.NodeType == HtmlAgilityPack.HtmlNodeType.Text)
                    {
                        var text = node.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(text) && text.Length > 5) // Likely address, not just whitespace
                            return NormalizeWhitespace(text);
                    }
                    else if (node.Name == "a" || node.Name == "div" || node.Name == "p")
                    {
                        var text = node.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(text) && text.Length > 5 && text.Contains(' '))
                            return NormalizeWhitespace(text);
                    }

                    node = node.NextSibling;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static string NormalizeWhitespace(string text)
    {
        return WhitespaceRegex.Replace(text, " ").Trim();
    }

    public async ValueTask DisposeAsync()
    {
        await _repository.DisposeAsync();
    }
}
