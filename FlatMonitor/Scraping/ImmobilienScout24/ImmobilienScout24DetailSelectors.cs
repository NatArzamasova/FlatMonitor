namespace FlatMonitor.Scraping.ImmobilienScout24;

/// <summary>
/// Selectors for extracting detail fields from ImmobilienScout24 expose pages.
/// Uses consistent is24qa-* class naming convention.
/// </summary>
public static class ImmobilienScout24DetailSelectors
{
    public const string TitleXPath = "//title/text()";
    public const string PropertyTypeXPath = ".//dd[@class='is24qa-typ grid-item three-fifths']//text()";
    public const string BuildYearXPath = ".//dd[@class='is24qa-baujahr grid-item three-fifths']//text()";
    public const string FloorXPath = ".//dd[contains(@class, 'is24qa-etage')]//text()";
    public const string HeatingXPath = ".//dd[@class='is24qa-heizungsart grid-item three-fifths']//text()";
    public const string EnergyCertificateTypeXPath = ".//dd[@class='is24qa-energieausweistyp grid-item three-fifths']//text()";
    public const string EnergyConsumptionXPath = ".//dd[@class='is24qa-endenergieverbrauch grid-item three-fifths']//text()";
    public const string EnergyEfficiencyClassXPath = ".//dd[@class='is24qa-energieeffizienzklasse grid-item three-fifths']//img[@alt]";
    public const string EnergyEfficiencyClassAttributeName = "alt";
    public const string PrimaryEnergySourceXPath = ".//dd[@class='is24qa-wesentliche-energietraeger grid-item three-fifths']//text()";
    public const string ServiceChargeXPath = ".//dd[@class='is24qa-hausgeld grid-item three-fifths']//text()";
    public const string AvailabilityXPath = ".//dd[contains(@class, 'is24qa-bezugsfrei-ab')]//text()";
    public const string RentedLabelXPath = "//*[@data-qa='is24qa-rented-label']";
}
