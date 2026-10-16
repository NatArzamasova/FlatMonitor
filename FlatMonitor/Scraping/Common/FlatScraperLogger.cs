namespace workInProgress.Scraping.Common;

using workInProgress.Models;

public static class FlatScraperLogger
{
    public static void LogEnrichedFlat(string source, Flat flat, int? currentIndex = null, int? totalCount = null)
    {
        var address = flat.Address ?? "N/A";
        var price = flat.PriceEur.HasValue ? $"{flat.PriceEur}€" : "N/A";
        var originalPrice = flat.OriginalPriceEur.HasValue ? $"{flat.OriginalPriceEur}€" : "N/A";
        var area = flat.LivingAreaSqm.HasValue ? $"{flat.LivingAreaSqm}m²" : "N/A";
        var rooms = flat.RoomsCount.HasValue ? $"{flat.RoomsCount}rooms" : "N/A";
        var commission = flat.Details?.CommissionPercent.HasValue ?? false
            ? $"{flat.Details.CommissionPercent}%"
            : "N/A";

        var countPart = currentIndex.HasValue && totalCount.HasValue
            ? $"({currentIndex}/{totalCount}) "
            : "";

        Console.WriteLine(
            $"{source}: {countPart}{flat.ListingId} - " +
            $"Address={address}, Price={price}, OriginalPrice={originalPrice}, " +
            $"Area={area}, Rooms={rooms}, Commission={commission}");
    }

    public static void LogEnrichmentFailed(string source, string listingId, string reason, int? currentIndex = null, int? totalCount = null)
    {
        var countPart = currentIndex.HasValue && totalCount.HasValue
            ? $"({currentIndex}/{totalCount}) "
            : "";

        Console.WriteLine(
            $"{source}: {countPart}{listingId} - Error enriching details: {reason}");
    }
}
