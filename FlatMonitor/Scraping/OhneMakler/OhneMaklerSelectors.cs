namespace FlatMonitor.Scraping.OhneMakler;

using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;


/// <summary>
/// OhneMakler-specific selectors for the FlatExtractor.
/// Implements IFlatSelectors for the common parser, plus static members for legacy code.
/// </summary>
public class OhneMaklerSelectors : IFlatSelectors
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
        { "Objektart", nameof(FlatDetails.PropertyType) },
        { "Baujahr", nameof(FlatDetails.BuildYear) },
        { "Etage", nameof(FlatDetails.Floor) },
        { "Geschoss", nameof(FlatDetails.Floor) },
        { "Zustand", nameof(FlatDetails.Condition) },
        { "Heizung", nameof(FlatDetails.Heating) },
        { "Nebenkosten", nameof(FlatDetails.ServiceChargeEur) },
        { "Energieausweistyp", nameof(FlatDetails.EnergyCertificateType) },
        { "Wesentlicher Energieträger", nameof(FlatDetails.PrimaryEnergySource) },
        { "Energieeffizienzklasse", nameof(FlatDetails.EnergyEfficiencyClass) },
        { "Endenergieverbrauch", nameof(FlatDetails.EnergyConsumption) },
        { "Privatangebot", nameof(FlatDetails.ListingStatus) },
        { "Gewerbliches Angebot", nameof(FlatDetails.ListingStatus) },
        { "Vermietet", nameof(FlatDetails.CurrentUse) },
        { "Eigennutzung", nameof(FlatDetails.CurrentUse) },
        { "Verfügbarkeit", nameof(FlatDetails.Availability) },
    };
}
