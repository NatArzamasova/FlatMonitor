namespace FlatMonitor.Scraping.ImmobilienScout24;

/// <summary>
/// Selectors for extracting detail fields from ImmobilienScout24 expose pages.
/// Uses consistent is24qa-* class naming convention.
/// </summary>
public class ImmobilienScout24DetailSelectors
{
    public string TitleXPath => "//title/text()";
    public string AddressXPath => "//*[@data-qa='is24-expose-address']//span[@class='address-text']";
    public string PropertyTypeXPath => ".//dd[@class='is24qa-typ grid-item three-fifths']//text()";
    public string BuildYearXPath => ".//dd[@class='is24qa-baujahr grid-item three-fifths']//text()";
    public string FloorXPath => ".//dd[contains(@class, 'is24qa-etage')]//text()";
    public string HeatingXPath => ".//dd[@class='is24qa-heizungsart grid-item three-fifths']//text()";
    public string EnergyCertificateTypeXPath => ".//dd[@class='is24qa-energieausweistyp grid-item three-fifths']//text()";
    public string EnergyConsumptionXPath => ".//dd[@class='is24qa-endenergieverbrauch grid-item three-fifths']//text()";
    public string EnergyEfficiencyClassXPath => ".//dd[@class='is24qa-energieeffizienzklasse grid-item three-fifths']//img[@alt]";
    public string EnergyEfficiencyClassAttributeName => "alt";
    public string PrimaryEnergySourceXPath => ".//dd[@class='is24qa-wesentliche-energietraeger grid-item three-fifths']//text()";
    public string ServiceChargeXPath => ".//dd[@class='is24qa-hausgeld grid-item three-fifths']//text()";
    public string AvailabilityXPath => ".//dd[contains(@class, 'is24qa-bezugsfrei-ab')]//text()";
}
