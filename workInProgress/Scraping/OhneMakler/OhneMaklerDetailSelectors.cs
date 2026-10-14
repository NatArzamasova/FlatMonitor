namespace workInProgress.Scraping.OhneMakler;

/// <summary>
/// Selectors for extracting detail fields from ohne-makler.net detail pages.
/// Supports three XPath patterns for robustness against markup variations:
/// 1. Label in &lt;td&gt; followed by value in next &lt;td&gt;
/// 2. Value &lt;span&gt; followed by label &lt;span&gt; (key facts pattern)
/// 3. Label &lt;div&gt; followed by value &lt;div&gt; (generic div pattern)
/// </summary>
public class OhneMaklerDetailSelectors
{
    private string BuildXPathPatterns(string label) => $"//tr[td/div[normalize-space()='{label}']]/td[2]/div|//span[normalize-space()='{label}']/preceding-sibling::span[@class]|//div[normalize-space()='{label}']/following-sibling::div";

    public string TitleXPath => "//title/text()";
    public string PropertyTypeXPath => BuildXPathPatterns("Objektart");
    public string BuildYearXPath => BuildXPathPatterns("Baujahr");
    public string FloorXPath => BuildXPathPatterns("Etage");
    public string FloorAlternate1XPath => BuildXPathPatterns("Geschoss");
    public string FloorAlternate2XPath => BuildXPathPatterns("Stockwerk");
    public string ConditionXPath => BuildXPathPatterns("Zustand");
    public string HeatingXPath => BuildXPathPatterns("Heizung");
    public string ServiceChargeHausgeldXPath => BuildXPathPatterns("Hausgeld mtl.");
    public string ServiceChargeNebenkostenXPath => BuildXPathPatterns("Nebenkosten");
    public string EnergyCertificateTypeXPath => BuildXPathPatterns("Energieausweistyp");
    public string PrimaryEnergySourceXPath => BuildXPathPatterns("Wesentlicher Energieträger");
    public string EnergyEfficiencyClassXPath => BuildXPathPatterns("Energieeffizienzklasse");
    public string EnergyConsumptionXPath => BuildXPathPatterns("Endenergieverbrauch");
    public string AvailabilityXPath => BuildXPathPatterns("Verfügbarkeit");
    public string AvailabilityAlternateXPath => BuildXPathPatterns("Übernahme ab");

    // Structure validation XPaths
    public string[] ValidStructurePatterns => new[]
    {
        "//tr",
        "//span[@class]",
        "//div[contains(text(), 'Heizung') or contains(text(), 'Verfügbarkeit')]"
    };
}
