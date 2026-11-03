namespace FlatMonitor.Scraping.Immowelt;

using System.Text.Json;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static ImmoweltJson;

/// <summary>One results page: the search's total count and the page's flats.</summary>
public sealed record ImmoweltListPage(int TotalCount, IReadOnlyList<Flat> Flats);

/// <summary>
/// Reads an immowelt results page from its data script (<see cref="ImmoweltPages.ListDataScript"/>): the lz-string
/// compressed <c>classified-serp-init-data</c>, whose <c>pageProps</c> list the page's flats in <c>classifieds</c> and
/// describe them in <c>classifiedsData</c>. Suggestions outside the search (<c>suggestedClassifieds</c>) are left out.
/// </summary>
public sealed class ImmoweltListParser
{
    private const string SearchDataKey = "classified-serp-init-data";

    /// <summary>A plain sale; auctions' prices are minimum bids or market values, not asking prices.</summary>
    private const string SaleDistributionType = "BUY";

    private readonly ILogger _logger;

    public ImmoweltListParser(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The page's flats, or <c>null</c> if the page has no search data (the DataDome guard, a changed page).</summary>
    public ImmoweltListPage? Parse(string html)
    {
        var scriptJson = ImmoweltPages.ReadDataScript(html, ImmoweltPages.ListDataScript);
        if (scriptJson == null)
            return null;

        using var script = JsonDocument.Parse(scriptJson);
        if (!script.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty(SearchDataKey, out var compressed)
            || compressed.ValueKind != JsonValueKind.String
            || LzString.DecompressFromBase64(compressed.GetString()) is not { Length: > 0 } searchJson)
        {
            return null;
        }

        return ParseSearchData(searchJson);
    }

    /// <summary>The flats of the decompressed search data. A flat that cannot be read is skipped.</summary>
    internal ImmoweltListPage ParseSearchData(string searchJson)
    {
        using var search = JsonDocument.Parse(searchJson);
        var pageProps = search.RootElement.GetProperty("pageProps");
        var totalCount = pageProps.TryGetProperty("totalCount", out var total) && total.TryGetInt32(out var count) ? count : 0;

        var flats = new List<Flat>();
        if (!pageProps.TryGetProperty("classifieds", out var ids) || !pageProps.TryGetProperty("classifiedsData", out var classifieds))
            return new ImmoweltListPage(totalCount, flats);

        foreach (var idElement in ids.EnumerateArray())
        {
            var id = idElement.GetString();
            if (string.IsNullOrEmpty(id) || !classifieds.TryGetProperty(id, out var classified))
            {
                _logger.LogDebug("Immowelt: listing {ListingId} has no data, skipped", id);
                continue;
            }

            try
            {
                var distributionType = Text(classified, "rawData", "distributionType");
                if (distributionType != SaleDistributionType)
                {
                    _logger.LogDebug("Immowelt: listing {ListingId} is no plain sale ({DistributionType}), left out", id, distributionType);
                    continue;
                }

                flats.Add(ToFlat(id, classified));
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException)
            {
                _logger.LogWarning(ex, "Immowelt: listing {ListingId} cannot be read, skipped", id);
            }
        }

        return new ImmoweltListPage(totalCount, flats);
    }

    /// <summary>
    /// The flat's own expose, <c>/expose/{metadata.legacyId}</c>: what <c>url</c> is for a single flat. A unit of a
    /// new-build project links to the project page instead (<c>/projekte/expose/{projectId}</c>), shared by all its
    /// units; its own expose is still there under its <c>legacyId</c>.
    /// </summary>
    private static string? ExposeUrl(JsonElement classified)
    {
        var url = Text(classified, "url");
        if (url == null || !url.Contains("/projekte/", StringComparison.OrdinalIgnoreCase))
            return url;

        return Text(classified, "metadata", "legacyId") is { } legacyId && Guid.TryParse(legacyId, out _)
            ? $"{ImmoweltPageInspection.BaseUrl}/expose/{legacyId}"
            : url;
    }

    private static Flat ToFlat(string id, JsonElement classified)
    {
        var price = Number(classified, "rawData", "price") is { } p ? (int)p : (int?)null;
        var previousPrice = GermanNumberParser.ParseInteger(Text(classified, "hardFacts", "price", "previousPrice"));

        return new Flat
        {
            Source = ImmoweltPages.SourceName,
            ListingId = id,
            Url = ExposeUrl(classified),
            Title = Text(classified, "mainDescription", "headline") ?? Text(classified, "hardFacts", "title"),
            Address = FormatAddress(Element(classified, "location", "address")),
            PostalCode = Text(classified, "location", "address", "zipCode"),
            District = Text(classified, "location", "address", "district"),
            PriceEur = price,
            OriginalPriceEur = previousPrice ?? price,
            RoomsCount = Number(classified, "rawData", "nbroom"),
            LivingAreaSqm = Number(classified, "rawData", "surface", "main"),
            ImageUrl = PreviewImageUrl(classified),
            ScrapedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// The first photo of <c>gallery.images</c> at preview size. The picture server scales to <c>?w=&amp;h=</c>: the full
    /// photo is about 150 KB, 400×300 about 14 KB. The expose's data script has no photos, so this is the flat's only one.
    /// </summary>
    internal static string? PreviewImageUrl(JsonElement classified)
    {
        if (Element(classified, "gallery", "images") is not { ValueKind: JsonValueKind.Array } images)
            return null;

        var url = images.EnumerateArray().Select(image => Text(image, "url")).FirstOrDefault(url => url != null);
        if (url == null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return new UriBuilder(uri) { Query = "w=400&h=300" }.Uri.AbsoluteUri;
    }
}
