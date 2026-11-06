namespace FlatMonitor.Models;

/// <summary>Fields from a flat's detail page. Title and commission live on <see cref="Flat"/>.</summary>
public sealed class FlatDetails
{
    /// <summary>
    /// A new instance with the same values, for a second row: EF tracks owned details per owner, so two rows must not
    /// share one instance. Shallow is enough: every property is a value or a string.
    /// </summary>
    public FlatDetails Copy() => (FlatDetails)MemberwiseClone();

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
