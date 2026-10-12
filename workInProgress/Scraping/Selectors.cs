namespace workInProgress.Scraping;

using workInProgress.Models;

internal static class Selectors
{
    public const string ListingsBaseUrl =
        "https://www.ohne-makler.net/immobilien/wohnung-kaufen/baden-wurttemberg/karlsruhe/";

    // Confirmed empirically: ?page=N is a real server-rendered pagination parameter.
    // page=1 and page=2 return disjoint listing sets; pages beyond the last one return zero listing links.
    public static string PageUrl(int pageNumber) =>
        pageNumber <= 1 ? ListingsBaseUrl : $"{ListingsBaseUrl}?page={pageNumber}";

    // Listing-card detail links look like /immobilie/{id}/
    public const string ListingLinkXPath = "//a[contains(@href, '/immobilie/')]";

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
        { "Bodenbelag", nameof(Details.Flooring) },
        { "Nebenkosten", nameof(Details.ServiceChargeEur) },
        { "Energieausweistyp", nameof(Details.EnergyCertificateType) },
        { "Wesentlicher Energieträger", nameof(Details.PrimaryEnergySource) },
        { "Ansprechpartner", nameof(Details.ContactName) },
        { "Privatangebot", nameof(Details.ListingStatus) },
        { "Gewerbliches Angebot", nameof(Details.ListingStatus) },
        { "Vermietet", nameof(Details.CurrentUse) },
        { "Eigennutzung", nameof(Details.CurrentUse) },
        { "Verfügbarkeit", nameof(Details.Availability) },
    };
}
