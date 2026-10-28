namespace FlatMonitor.Models;

/// <summary>
/// The Karlsruhe districts the monitor tracks. Scraped flats outside these districts are dropped,
/// and the default district filter may only contain these names.
/// </summary>
public static class KarlsruheDistricts
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Beiertheim-Bulach",
        "Daxlanden",
        "Durlach",
        "Grötzingen",
        "Grünwettersbach",
        "Grünwinkel",
        "Hagsfeld",
        "Hohenwettersbach",
        "Innenstadt-Ost",
        "Innenstadt-West",
        "Knielingen",
        "Mühlburg",
        "Neureut",
        "Nordstadt",
        "Nordweststadt",
        "Oberreut",
        "Oststadt",
        "Palmbach",
        "Rintheim",
        "Rüppurr",
        "Stupferich",
        "Südstadt",
        "Südweststadt",
        "Waldstadt",
        "Weiherfeld-Dammerstock",
        "Weststadt",
        "Wolfartsweier"
    };

    /// <summary>
    /// Returns the canonical spelling of <paramref name="name"/> on an exact, case-insensitive match;
    /// otherwise <c>null</c>. Unlike scrape-time normalization, no substring matching is done.
    /// </summary>
    public static string? ToCanonical(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return All.FirstOrDefault(d => d.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
