namespace workInProgress.Scraping.OhneMakler;

using workInProgress.Models;

public static class OhneMaklerSelectors
{
    public const string ListingsBaseUrl =
        "https://www.ohne-makler.net/immobilien/wohnung-kaufen/baden-wurttemberg/karlsruhe/";

    // Confirmed empirically: ?page=N is a real server-rendered pagination parameter.
    // page=1 and page=2 return disjoint listing sets; pages beyond the last one return zero listing links.
    public static string PageUrl(int pageNumber) =>
        pageNumber <= 1 ? ListingsBaseUrl : $"{ListingsBaseUrl}?page={pageNumber}";

    // Listing-card detail links look like /immobilie/{id}/
    public const string ListingLinkXPath = "//a[contains(@href, '/immobilie/')]";

    // Karlsruhe postcode whitelist. Scraper stops pagination when encountering a listing outside this list.
    // Includes all districts and Postfach (76006).
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

    // German detail-page labels (exact wording from the site) → Details field mappings.
    // Used by DetailPageParser to extract label→value pairs generically.
    internal static readonly Dictionary<string, string> LabelToDetailsField = new()
    {
        { "Objektart", nameof(Details.PropertyType) },
        { "Baujahr", nameof(Details.BuildYear) },
        { "Etage", nameof(Details.Floor) },
        { "Geschoss", nameof(Details.Floor) },
        { "Zustand", nameof(Details.Condition) },
        { "Heizung", nameof(Details.Heating) },
        { "Nebenkosten", nameof(Details.ServiceChargeEur) },
        { "Energieausweistyp", nameof(Details.EnergyCertificateType) },
        { "Wesentlicher Energieträger", nameof(Details.PrimaryEnergySource) },
        { "Energieeffizienzklasse", nameof(Details.EnergyEfficiencyClass) },
        { "Endenergieverbrauch", nameof(Details.EnergyConsumption) },
        { "Privatangebot", nameof(Details.ListingStatus) },
        { "Gewerbliches Angebot", nameof(Details.ListingStatus) },
        { "Vermietet", nameof(Details.CurrentUse) },
        { "Eigennutzung", nameof(Details.CurrentUse) },
        { "Verfügbarkeit", nameof(Details.Availability) },
    };
}
