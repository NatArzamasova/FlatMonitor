namespace workInProgress.Scraping.Common;

using workInProgress.Models;

public static class DetailScraperLogger
{
    public static void LogEnrichedListing(string source, Flat listing, int? currentIndex = null, int? totalCount = null)
    {
        var address = listing.Address ?? "N/A";
        var price = listing.PriceEur.HasValue ? $"{listing.PriceEur}€" : "N/A";
        var originalPrice = listing.OriginalPriceEur.HasValue ? $"{listing.OriginalPriceEur}€" : "N/A";
        var area = listing.LivingAreaSqm.HasValue ? $"{listing.LivingAreaSqm}m²" : "N/A";
        var rooms = listing.RoomsCount.HasValue ? $"{listing.RoomsCount}rooms" : "N/A";
        var commission = listing.Details?.CommissionPercent.HasValue ?? false
            ? $"{listing.Details.CommissionPercent}%"
            : "N/A";

        var countPart = currentIndex.HasValue && totalCount.HasValue
            ? $"({currentIndex}/{totalCount}) "
            : "";

        Console.WriteLine(
            $"{source}: {countPart}{listing.ListingId} - " +
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
