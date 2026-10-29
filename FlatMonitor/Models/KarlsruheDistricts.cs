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
    /// otherwise <c>null</c>. Used for user input; see <see cref="Match"/> for scraped names.
    /// </summary>
    public static string? ToCanonical(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return All.FirstOrDefault(d => d.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Maps a scraped district name to its canonical spelling, or <c>null</c> if it is not a tracked district.
    /// Tries an exact match first, then a district contained in the name ("KA-Oberreut" → "Oberreut").
    /// If several districts are contained, the longest wins ("KA-Nordweststadt" → "Nordweststadt", not "Weststadt").
    /// </summary>
    public static string? Match(string? scrapedName)
    {
        if (string.IsNullOrWhiteSpace(scrapedName))
            return null;

        return ToCanonical(scrapedName)
            ?? All.Where(d => scrapedName.Contains(d, StringComparison.OrdinalIgnoreCase))
                .MaxBy(d => d.Length);
    }
}
