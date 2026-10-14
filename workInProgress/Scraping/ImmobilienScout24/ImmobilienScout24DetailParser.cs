namespace workInProgress.Scraping.ImmobilienScout24;

using HtmlAgilityPack;
using workInProgress.Models;
using workInProgress.Storage;

/// <summary>
/// Parses detail fields from ImmobilienScout24 expose page HTML.
/// Implements IDetailParser for unified multi-source architecture.
/// </summary>
public class ImmobilienScout24DetailParser : IDetailParser
{
    private readonly ImmobilienScout24DetailSelectors _selectors;

    public ImmobilienScout24DetailParser()
    {
        _selectors = new ImmobilienScout24DetailSelectors();
    }

    public Details? ParseDetailFields(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var root = doc.DocumentNode;

            var address = DetailExtractors.ExtractText(doc, _selectors.AddressXPath);
            var propertyType = DetailExtractors.ExtractText(doc, _selectors.PropertyTypeXPath);
            var buildYear = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.BuildYearXPath));
            var heating = DetailExtractors.ExtractText(doc, _selectors.HeatingXPath);
            var energyCertType = DetailExtractors.ExtractText(doc, _selectors.EnergyCertificateTypeXPath);
            var energyConsumption = DetailExtractors.ExtractText(doc, _selectors.EnergyConsumptionXPath);
            var energyEffClass = DetailExtractors.ExtractText(doc, _selectors.EnergyEfficiencyClassXPath);
            var primaryEnergy = DetailExtractors.ExtractText(doc, _selectors.PrimaryEnergySourceXPath);

            var details = new Details
            {
                PropertyType = propertyType,
                BuildYear = buildYear,
                Heating = heating,
                EnergyCertificateType = energyCertType,
                EnergyConsumption = energyConsumption,
                EnergyEfficiencyClass = energyEffClass,
                PrimaryEnergySource = primaryEnergy,
            };

            return details;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing detail fields: {ex.Message}");
            return null;
        }
    }

    public string? ExtractAddress(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return DetailExtractors.ExtractText(doc, _selectors.AddressXPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error extracting address: {ex.Message}");
            return null;
        }
    }
}
