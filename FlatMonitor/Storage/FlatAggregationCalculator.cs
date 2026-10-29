namespace FlatMonitor.Storage;

using FlatMonitor.Models;

public static class FlatAggregationCalculator
{
    /// <summary>
    /// The average price per m² by district and rooms, over the flats that have one (a price and a living area).
    /// <see cref="DailyFlatAggregation.Count"/> is the number of flats in that average, so a group never shows a
    /// count without an average. Every published flat is in a tracked district: the coordinator drops the others.
    /// </summary>
    public static List<DailyFlatAggregation> Compute(IEnumerable<PublishedFlat> flats)
    {
        return flats
            .Where(f => f.PricePerMeter is > 0)
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
}
