namespace FlatMonitor.Models;

/// <summary>
/// The Karlsruhe districts the monitor tracks. Scraped flats outside these districts are dropped,
/// and the default district filter may only contain these names.
/// </summary>
public static class KarlsruheDistricts
{
    // A HashSet internally, so ToCanonical can look a name up and get its stored spelling back
    private static readonly HashSet<string> Districts = new(StringComparer.OrdinalIgnoreCase)
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

    public static IReadOnlySet<string> All => Districts;

    /// <summary>
    /// Returns the canonical spelling of <paramref name="name"/> on an exact, case-insensitive match;
    /// otherwise <c>null</c>. Used for user input; see <see cref="Match"/> for scraped names.
    /// </summary>
    public static string? ToCanonical(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return Districts.TryGetValue(name.Trim(), out var canonical) ? canonical : null;
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
