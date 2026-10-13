namespace workInProgress.Scraping.OhneMakler;

using System.Text.RegularExpressions;
using workInProgress.Models;
using workInProgress.Storage;

public sealed class OhneMaklerScraper : IScraper
{
    public const string SourceName = "OhneMakler";

    private readonly HttpClient _httpClient;
    private readonly OhneMaklerListingsPageFetcher _pageFetcher;
    private readonly OhneMaklerListingCardParser _cardParser;
    private readonly OhneMaklerDetailPageParser _detailParser;

    public string Source => SourceName;

    public OhneMaklerScraper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _pageFetcher = new OhneMaklerListingsPageFetcher(_httpClient);
        _cardParser = new OhneMaklerListingCardParser();
        _detailParser = new OhneMaklerDetailPageParser();
    }

    public async Task<IEnumerable<Flat>> ScrapeAsync() => await ScrapeAllListingsAsync();

    public async Task<List<Flat>> ScrapeAllListingsAsync(CancellationToken ct = default)
    {
        var allListings = new List<Flat>();
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
                    !OhneMaklerSelectors.KarlsruhePostcodes.Contains(listing.PostalCode))
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

                    // Extract title, address, rooms, and area from the detail page HTML.
                    string? title = ExtractTitleFromDetailPage(detailHtml);
                    string? address = ExtractAddressFromDetailPage(detailHtml);
                    int? rooms = ExtractRoomsFromDetailPage(detailHtml);
                    int? area = ExtractAreaFromDetailPage(detailHtml);

                    // Update the listing with detail-page data.
                    var absoluteUrl = listing.Url.StartsWith("http")
                        ? listing.Url
                        : new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), listing.Url).AbsoluteUri;

                    var enrichedFlat = new Flat
                    {
                        ListingId = listing.ListingId,
                        Url = absoluteUrl,
                        Title = title ?? listing.Title,
                        Address = address,
                        PostalCode = listing.PostalCode,
                        District = listing.District,
                        PriceEur = listing.PriceEur,
                        RoomsCount = rooms ?? listing.RoomsCount,
                        LivingAreaSqm = area ?? listing.LivingAreaSqm,
                        Details = details,
                        ScrapedAtUtc = listing.ScrapedAtUtc,
                        Source = SourceName,
                    };

                    allListings.Add(enrichedFlat);
                }
                catch (Exception ex)
                {
                    // Per confirmed behavior: log warning, still include listing with partial data.
                    var absoluteUrl = listing.Url.StartsWith("http")
                        ? listing.Url
                        : new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), listing.Url).AbsoluteUri;

                    var partialFlat = new Flat
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
                        Source = SourceName,
                    };

                    Console.WriteLine(
                        $"Warning: Failed to fetch/parse detail page for listing {listing.ListingId} ({absoluteUrl}): {ex.Message}");
                    allListings.Add(partialFlat);
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
            : new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), relativeUrl).AbsoluteUri;

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
                            return OhneMaklerAddressHelper.SanitizeAddress(NormalizeWhitespace(text));
                    }
                    else if (node.Name == "a" || node.Name == "div" || node.Name == "p")
                    {
                        var text = node.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(text) && text.Length > 5 && text.Contains(' '))
                            return OhneMaklerAddressHelper.SanitizeAddress(NormalizeWhitespace(text));
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

    private static int? ExtractRoomsFromDetailPage(string html)
    {
        try
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Look for text containing "Zimmer" and extract the number before it
            var pageText = doc.DocumentNode.InnerText;
            var match = System.Text.RegularExpressions.Regex.Match(pageText, @"(\d+(?:,\d+)?)\s*(?:Zimmer|Zi)(?:\s|$)");
            if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ".").Split('.')[0], out int rooms))
                return rooms;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static int? ExtractAreaFromDetailPage(string html)
    {
        try
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Look for "Wohnfläche" label and extract the preceding number with m² or m2
            var wohnflaecheLabel = doc.DocumentNode.SelectSingleNode("//span[contains(normalize-space(), 'Wohnfläche')]");
            if (wohnflaecheLabel != null)
            {
                var prevSpan = wohnflaecheLabel.PreviousSibling;
                while (prevSpan != null)
                {
                    if (prevSpan.Name == "span")
                    {
                        var text = prevSpan.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(text) && int.TryParse(System.Text.RegularExpressions.Regex.Replace(text, @"[^\d]", ""), out int area))
                            return area;
                        break;
                    }
                    prevSpan = prevSpan.PreviousSibling;
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
}
