namespace workInProgress.Scraping.ImmobilienScout24;

using HtmlAgilityPack;
using workInProgress.Models;

/// <summary>
/// Parses detail fields from ImmobilienScout24 expose page HTML.
/// </summary>
public class ImmobilienScout24DetailParser
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

            var address = ExtractText(root, _selectors.AddressXPath);
            var propertyType = ExtractText(root, _selectors.PropertyTypeXPath);
            var buildYear = ParseInt(ExtractText(root, _selectors.BuildYearXPath));
            var heating = ExtractText(root, _selectors.HeatingXPath);
            var energyCertType = ExtractText(root, _selectors.EnergyCertificateTypeXPath);
            var energyConsumption = ExtractText(root, _selectors.EnergyConsumptionXPath);
            var energyEffClass = ExtractText(root, _selectors.EnergyEfficiencyClassXPath);
            var primaryEnergy = ExtractText(root, _selectors.PrimaryEnergySourceXPath);

            Console.WriteLine($"Extracted: Address={address}, PropertyType={propertyType}, BuildYear={buildYear}, Heating={heating}");
            Console.WriteLine($"Extracted: EnergyCertType={energyCertType}, EnergyConsumption={energyConsumption}");
            Console.WriteLine($"Extracted: EnergyEffClass={energyEffClass}, PrimaryEnergy={primaryEnergy}");

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
            var root = doc.DocumentNode;
            return ExtractText(root, _selectors.AddressXPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error extracting address: {ex.Message}");
            return null;
        }
    }

    private string? ExtractText(HtmlNode node, string xpath)
    {
        try
        {
            var text = node.SelectSingleNode(xpath)?.InnerText?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private int? ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Extract first number sequence
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
        return match.Success && int.TryParse(match.Value, out var value) ? value : null;
    }
}
