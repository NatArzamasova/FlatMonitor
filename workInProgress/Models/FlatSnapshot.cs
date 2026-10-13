namespace workInProgress.Models;

public sealed class FlatSnapshot
{
    public required string Source { get; init; }
    public required string ListingId { get; init; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? District { get; set; }
    public int? PriceEur { get; set; }
    public int? RoomsCount { get; set; }
    public int? LivingAreaSqm { get; set; }
    public Details? Details { get; set; }
    public required string ScrapedAtUtc { get; set; }
}
