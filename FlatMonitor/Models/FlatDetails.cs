namespace FlatMonitor.Models;

/// <summary>Fields from a flat's detail page. Title and commission live on <see cref="Flat"/>.</summary>
public sealed class FlatDetails
{
    public string? PropertyType { get; init; }
    public int? BuildYear { get; init; }
    public string? Floor { get; init; }
    public string? Condition { get; init; }
    public string? Heating { get; init; }
    public int? ServiceChargeEur { get; init; }
    public string? EnergyCertificateType { get; init; }
    public string? PrimaryEnergySource { get; init; }
    public string? EnergyEfficiencyClass { get; init; }
    public string? EnergyConsumption { get; init; }
    public string? ListingStatus { get; init; }
    public string? CurrentUse { get; init; }
    public string? Availability { get; init; }
}
