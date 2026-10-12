namespace workInProgress.Models;

public sealed class Listing
{
    public required string ListingId { get; init; }
    public required string Url { get; init; }
    public string? Title { get; init; }
    public string? Address { get; init; }
    public string? PostalCode { get; init; }
    public string? District { get; init; }
    public int? PriceEur { get; init; }
    public int? RoomsCount { get; init; }
    public int? LivingAreaSqm { get; init; }
    public Details? Details { get; init; }
    public required string ScrapedAtUtc { get; init; }
}
