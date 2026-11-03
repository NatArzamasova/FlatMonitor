namespace FlatMonitor.Storage;

using System.Net;
using System.Text.RegularExpressions;
using FlatMonitor.Models;

public static class FlatAggregationCalculator
{
    /// <summary>
    /// The average price per m² by district and rooms, over the flats that have one (a price and a living area).
    /// <see cref="DailyFlatAggregation.Count"/> is the number of flats in that average, so a group never shows a
    /// count without an average. Every published flat is in a tracked district: the coordinator drops the others.
    /// A flat listed on several sources is counted once (see <see cref="RemoveCrossSourceDuplicates"/>).
    /// </summary>
    public static List<DailyFlatAggregation> Compute(IEnumerable<PublishedFlat> flats)
    {
        var priced = flats.Where(f => f.PricePerMeter is > 0);

        return RemoveCrossSourceDuplicates(priced)
            .GroupBy(f => new { f.District, f.RoomsCount })
            .Select(g => new DailyFlatAggregation
            {
                District = g.Key.District,
                RoomsCount = g.Key.RoomsCount,
                Count = g.Count(),
                AvgPricePerMeter = (decimal)g.Average(f => f.PricePerMeter!.Value),
            })
            .OrderBy(a => a.District)
            .ThenBy(a => a.RoomsCount)
            .ToList();
    }

    /// <summary>
    /// Flats with the same district, price, living area and title (see <see cref="NormalizeTitle"/>) but from
    /// different sources are taken as one flat listed on several portals. Within one source they stay different
    /// flats (identical units in a new building, with a reused title), so such a group keeps as many flats as its
    /// source with the most of them: the flats of that source (on a tie, the first source by name), which also
    /// decides the rooms group. A flat without a title is never a duplicate.
    /// </summary>
    private static IEnumerable<PublishedFlat> RemoveCrossSourceDuplicates(IEnumerable<PublishedFlat> flats)
    {
        return flats
            .GroupBy(f => NormalizeTitle(f.Title) is { Length: > 0 } title
                ? $"{f.District}|{f.PriceEur}|{f.LivingAreaSqm}|{title}"
                : $"no title|{f.Source}|{f.ListingId}")
            .SelectMany(g => g
                .GroupBy(f => f.Source)
                .OrderByDescending(s => s.Count())
                .ThenBy(s => s.Key, StringComparer.Ordinal)
                .First());
    }

    /// <summary>
    /// The title without what the portals write differently: HTML entities (&amp;amp; on one, &amp; on another),
    /// dashes, punctuation, case and spaces.
    /// </summary>
    internal static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "";

        var words = Regex.Split(WebUtility.HtmlDecode(title).ToLowerInvariant(), @"[^\w]+")
            .Where(w => w.Length > 0);
        return string.Join(' ', words);
    }
}
