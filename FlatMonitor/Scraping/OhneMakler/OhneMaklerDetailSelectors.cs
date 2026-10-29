namespace FlatMonitor.Scraping.OhneMakler;

/// <summary>
/// Selectors for extracting detail fields from ohne-makler.net detail pages.
/// Supports three XPath patterns for robustness against markup variations:
/// 1. Label in &lt;td&gt; followed by value in next &lt;td&gt;
/// 2. Value &lt;span&gt; followed by label &lt;span&gt; (key facts pattern)
/// 3. Label &lt;div&gt; followed by value &lt;div&gt; (generic div pattern)
/// </summary>
public static class OhneMaklerDetailSelectors
{
    private static string BuildXPathPatterns(string label) => $"//tr[td/div[normalize-space()='{label}']]/td[2]/div|//span[normalize-space()='{label}']/preceding-sibling::span[@class]|//div[normalize-space()='{label}']/following-sibling::div";

    public const string TitleXPath = "//title/text()";
    public static readonly string PropertyTypeXPath = BuildXPathPatterns("Objektart");
    public static readonly string BuildYearXPath = BuildXPathPatterns("Baujahr");
    public static readonly string FloorXPath = BuildXPathPatterns("Etage");
    public static readonly string FloorAlternate1XPath = BuildXPathPatterns("Geschoss");
    public static readonly string FloorAlternate2XPath = BuildXPathPatterns("Stockwerk");
    public static readonly string ConditionXPath = BuildXPathPatterns("Zustand");
    public static readonly string HeatingXPath = BuildXPathPatterns("Heizung");
    public static readonly string ServiceChargeHausgeldXPath = BuildXPathPatterns("Hausgeld mtl.");
    public static readonly string ServiceChargeNebenkostenXPath = BuildXPathPatterns("Nebenkosten");
    public static readonly string EnergyCertificateTypeXPath = BuildXPathPatterns("Energieausweistyp");
    public static readonly string PrimaryEnergySourceXPath = BuildXPathPatterns("Wesentlicher Energieträger");
    public static readonly string EnergyEfficiencyClassXPath = BuildXPathPatterns("Energieeffizienzklasse");
    public static readonly string EnergyConsumptionXPath = BuildXPathPatterns("Endenergieverbrauch");
    public static readonly string AvailabilityXPath = BuildXPathPatterns("Verfügbarkeit");
    public static readonly string AvailabilityAlternateXPath = BuildXPathPatterns("Übernahme ab");
    public static readonly string PriceXPath = BuildXPathPatterns("Kaufpreis");
    public static readonly string CurrentUseXPath = BuildXPathPatterns("Aktuelle Nutzung");
    public static readonly string UsageXPath = BuildXPathPatterns("Nutzung");

    // Structure validation XPaths
    public static readonly string[] ValidStructurePatterns =
    [
        "//tr",
        "//span[@class]",
        "//div[contains(text(), 'Heizung') or contains(text(), 'Verfügbarkeit')]",
    ];
}
