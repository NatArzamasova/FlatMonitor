namespace FlatMonitor.Models;

public sealed class FlatDetails
{
    public string? Title { get; init; }
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
    public double? CommissionPercent { get; init; }
}
