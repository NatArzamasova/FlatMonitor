namespace FlatMonitor.Scraping.OhneMakler;

using FlatMonitor.Models;

/// <summary>
/// ohne-makler.net URLs, the listing link selector, Karlsruhe's postcodes and the detail page labels.
/// </summary>
public static class OhneMaklerSelectors
{
    public const string ListingsBaseUrl =
        "https://www.ohne-makler.net/immobilien/wohnung-kaufen/baden-wurttemberg/karlsruhe/";

    public static string PageUrl(int pageNumber) =>
        pageNumber <= 1 ? ListingsBaseUrl : $"{ListingsBaseUrl}?page={pageNumber}";

    public const string ListingLinkXPath = "//a[contains(@href, '/immobilie/')]";

    public static readonly HashSet<string> KarlsruhePostcodes = new()
    {
        "76006", // Post Office Box (Postfach)
        "76131", // Innenstadt-Ost, Oststadt, Rintheim
        "76133", // Innenstadt-West, Weststadt
        "76135", // Beiertheim-Bulach, Oberreut, Südweststadt
        "76137", // Südstadt, Südweststadt
        "76139", // Hagsfeld, Waldstadt
        "76149", // Neureut
        "76185", // Mühlburg, Weststadt
        "76187", // Daxlanden
        "76189", // Grünwinkel
        "76199", // Weiherfeld-Dammerstock, Rüppurr
        "76227", // Durlach, Aue
        "76228", // Hohenwettersbach, Stupferich, Wolfartsweier, Grünwettersbach
        "76229", // Kleinsteinbach, Palmbach
    };
}
