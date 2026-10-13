namespace workInProgress.Scraping.OhneMakler;

using workInProgress.Models;
using workInProgress.Scraping.Common;

/// <summary>
/// OhneMakler-specific selectors for the ListingExtractor.
/// Implements IListingSelectors for the common parser, plus static members for legacy code.
/// </summary>
public class OhneMaklerSelectors : IListingSelectors
{
    // IListingSelectors interface implementation
    public string ListingCardXPath => "//div[@class='angebot']";

    public string IdXPath => ".//a/@href[contains(., '/immobilie/')]";

    public string TitleXPath => ".//h2[@class='headline']/text()";

    public string PriceXPath => ".//span[@class='price']/text()";

    public string SizeXPath => ".//span[@class='objekttyp']/text()";

    public string AddressXPath => ".//p[@class='location']/text()";

    public string? RoomsXPath => ".//span[@class='rooms']/text()";

    public string DetailUrlXPath => ".//a/@href[contains(., '/immobilie/')]";

    public string BaseUrl => "https://www.ohne-makler.net";

    // Static members for legacy code
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
