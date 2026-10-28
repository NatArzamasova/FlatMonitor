namespace FlatMonitor.Scraping.OhneMakler;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using FlatMonitor.Storage;


/// <summary>
/// Scraper for ohne-makler.net flats.
/// Uses HttpClient for fetching pages, processes each listing's details immediately.
/// Saves flats to database in batches to avoid memory bloat on large datasets.
/// </summary>
public sealed class OhneMaklerScraper : FlatScraper
{
    public const string SourceName = "OhneMakler";

    private readonly HttpClient _httpClient;
    private readonly string _htmlSnapshotsDir;
    private OhneMaklerFlatsPageFetcher? _pageFetcher;
    private readonly OhneMaklerFlatCardParser _cardParser;
    private readonly OhneMaklerDetailPageParser _detailParser;

    public override string Source => SourceName;

    public OhneMaklerScraper(HttpClient httpClient, StorageOptions storage)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _htmlSnapshotsDir = Path.Combine(storage.DataDirectory, "snapshots", "html", SourceName);
        _cardParser = new OhneMaklerFlatCardParser();
        _detailParser = new OhneMaklerDetailPageParser();
    }

    protected override Task InitializeAsync(CancellationToken ct)
    {
        _pageFetcher = new OhneMaklerFlatsPageFetcher(_httpClient);
        return Task.CompletedTask;
    }

    protected override Task ClearPreviousSnapshotsAsync(CancellationToken ct)
    {
        try
        {
            var htmlDir = _htmlSnapshotsDir;

            if (Directory.Exists(htmlDir))
            {
                var files = Directory.GetFiles(htmlDir, "*.html");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                Console.WriteLine($"Cleared {files.Length} previous HTML snapshots from {htmlDir}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not clear previous HTML snapshots: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    protected override async IAsyncEnumerable<List<Flat>> FetchAndParseAllPagesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        if (_pageFetcher == null)
        {
            yield break;
        }

        bool shouldStop = false;
        int pageNumber = 0;

        // Iterate through all results pages.
        await foreach (var pageHtml in _pageFetcher.FetchAllPagesHtmlAsync(ct))
        {
            pageNumber++;
            var pageListings = new List<Flat>();

            // Parse listing cards from this page.
            var cardListings = _cardParser.ParsePage(pageHtml);

            foreach (var (listing, detailPageUrl) in cardListings)
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

                pageListings.Add(listing);
            }

            // Return listings for this page
            yield return pageListings;

            if (shouldStop)
                yield break;
        }
    }

    protected override async Task<Flat> EnrichListingWithDetailsAsync(Flat baseListing, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(baseListing.DetailPageUrl))
        {
            return baseListing;
        }

        try
        {
            // Fetch and parse the detail page.
            string detailHtml = await FetchDetailPageAsync(baseListing.DetailPageUrl, ct);
            var details = _detailParser.ParseDetailFields(detailHtml);

            // Extract all data from detail page (detail page is the source of truth)
            string? title = ExtractTitleFromDetailPage(detailHtml);
            string? address = ExtractAddressFromDetailPage(detailHtml);
            int? rooms = ExtractRoomsFromDetailPage(detailHtml);
            double? area = ExtractAreaFromDetailPage(detailHtml);
            int? priceFromDetail = _detailParser.ExtractPrice(detailHtml);

            // Update the listing with detail-page data.
            var absoluteUrl = baseListing.Url?.StartsWith("http") ?? false
                ? baseListing.Url
                : new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), baseListing.Url ?? "").AbsoluteUri;

            var enrichedFlat = new Flat
            {
                ListingId = baseListing.ListingId,
                Url = absoluteUrl,
                Title = title ?? baseListing.Title,
                Address = address,
                PostalCode = baseListing.PostalCode,
                District = baseListing.District,
                PriceEur = priceFromDetail ?? baseListing.PriceEur,
                OriginalPriceEur = priceFromDetail ?? baseListing.PriceEur,
                RoomsCount = rooms ?? baseListing.RoomsCount,
                LivingAreaSqm = area ?? baseListing.LivingAreaSqm,
                Details = details,
                ScrapedAtUtc = baseListing.ScrapedAtUtc,
                Source = SourceName,
                DetailPageUrl = baseListing.DetailPageUrl,
            };

            return enrichedFlat;
        }
        catch (Exception ex)
        {
            // Per confirmed behavior: log warning, still include listing with partial data.
            var absoluteUrl = baseListing.Url?.StartsWith("http") ?? false
                ? baseListing.Url
                : new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), baseListing.Url ?? "").AbsoluteUri;

            var partialFlat = new Flat
            {
                ListingId = baseListing.ListingId,
                Url = absoluteUrl,
                Title = baseListing.Title,
                Address = null,
                PostalCode = baseListing.PostalCode,
                District = baseListing.District,
                PriceEur = baseListing.PriceEur,
                OriginalPriceEur = baseListing.PriceEur,
                RoomsCount = baseListing.RoomsCount,
                LivingAreaSqm = baseListing.LivingAreaSqm,
                Details = baseListing.Details,
                ScrapedAtUtc = baseListing.ScrapedAtUtc,
                Source = SourceName,
                DetailPageUrl = baseListing.DetailPageUrl,
            };

            Console.WriteLine(
                $"Warning: Failed to fetch/parse detail page for listing {baseListing.ListingId} ({absoluteUrl}): {ex.Message}");
            return partialFlat;
        }
    }

    protected override async Task CleanupAsync()
    {
        // HttpClient is a DI singleton, managed by container — do not dispose
    }

    private async Task<string> FetchDetailPageAsync(string relativeUrl, CancellationToken ct)
    {
        var absoluteUrl = relativeUrl.StartsWith("http")
            ? relativeUrl
            : new Uri(new Uri(OhneMaklerSelectors.ListingsBaseUrl), relativeUrl).AbsoluteUri;

        var response = await _httpClient.GetAsync(absoluteUrl, ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);

        // Save HTML snapshot
        _ = Task.Run(() => SaveDetailHtmlSnapshotAsync(absoluteUrl, html), ct);

        return html;
    }

    private async Task SaveDetailHtmlSnapshotAsync(string url, string html)
    {
        try
        {
            var htmlDir = _htmlSnapshotsDir;
            if (!Directory.Exists(htmlDir))
            {
                Directory.CreateDirectory(htmlDir);
            }

            var listingId = ExtractListingIdFromUrl(url);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{listingId}_{timestamp}.html";
            var filePath = Path.Combine(htmlDir, fileName);

            await File.WriteAllTextAsync(filePath, html);
        }
        catch
        {
            // Silently fail - don't interrupt scraping for snapshot saving
        }
    }

    private static string ExtractListingIdFromUrl(string url)
    {
        // Extract listing ID from URL like: https://ohne-makler.net/immobilie/502814/
        var parts = url.Split('/');
        var immobilieIndex = Array.IndexOf(parts, "immobilie");
        if (immobilieIndex >= 0 && immobilieIndex < parts.Length - 1)
        {
            return parts[immobilieIndex + 1];
        }
        // Fallback to hash of URL if ID cannot be extracted
        return Math.Abs(url.GetHashCode()).ToString();
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

    private static double? ExtractAreaFromDetailPage(string html)
    {
        try
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Search for any element containing "m²" or "m2" and extract the area from it
            // This directly finds spans like <span>17,90 m²</span>
            var nodes = doc.DocumentNode.SelectNodes("//*[contains(., 'm²') or contains(., 'm2')]");
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    var text = node.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Match pattern: number with optional comma/period followed by m² or m2
                        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+[.,]\d+|\d+)\s*(?:m²|m2)");
                        if (match.Success)
                        {
                            var numericPart = match.Groups[1].Value.Replace(",", ".");
                            if (double.TryParse(numericPart, System.Globalization.CultureInfo.InvariantCulture, out double area) && area > 0)
                            {
                                // Found a valid area - check if it's reasonable (between 5 and 500 sqm for apartments)
                                if (area >= 5 && area <= 500)
                                    return area;
                            }
                        }
                    }
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
