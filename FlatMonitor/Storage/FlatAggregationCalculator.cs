namespace workInProgress.Storage;

using workInProgress.Models;

public static class FlatAggregationCalculator
{
    public static List<FlatAggregationSnapshot> Compute(IEnumerable<FlatSnapshot> snapshots)
    {
        return snapshots
            .Where(f => f.District != null && f.PriceEur.HasValue && f.PriceEur > 0)
            .GroupBy(f => new { f.District, f.RoomsCount })
            .Select(g => new FlatAggregationSnapshot
            {
                District = g.Key.District,
                RoomsCount = g.Key.RoomsCount,
                Count = g.Count(),
                AvgPricePerMeter = g.Where(f => f.PricePerMeter.HasValue).Any()
                    ? (decimal)g.Where(f => f.PricePerMeter.HasValue).Average(f => f.PricePerMeter!.Value)
                    : 0m
            })
            .OrderBy(a => a.District)
            .ThenBy(a => a.RoomsCount)
            .ToList();
    }
}
