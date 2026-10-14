namespace workInProgress.Scraping.ImmobilienScout24;

using System.Text.Json;
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
            var energyEffClass = DetailExtractors.ExtractAttribute(doc, _selectors.EnergyEfficiencyClassXPath, _selectors.EnergyEfficiencyClassAttributeName);
            var primaryEnergy = DetailExtractors.ExtractText(doc, _selectors.PrimaryEnergySourceXPath);

            // Extract commission percent from expose metadata
            var metadata = ExtractExposeMetadata(html);
            var commissionPercent = metadata?.CommissionPercent;

            var details = new Details
            {
                PropertyType = propertyType,
                BuildYear = buildYear,
                Heating = heating,
                EnergyCertificateType = energyCertType,
                EnergyConsumption = energyConsumption,
                EnergyEfficiencyClass = energyEffClass,
                PrimaryEnergySource = primaryEnergy,
                CommissionPercent = commissionPercent,
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

    /// <summary>
    /// Extract listing metadata (address, postal code, district, living area, rooms, price, commission) from embedded JSON in page.
    /// The page contains a script tag with commissionSplitConfig object containing courtage and expose sub-object.
    /// </summary>
    public ExposeMetadata? ExtractExposeMetadata(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var scriptNodes = doc.DocumentNode.SelectNodes("//script[contains(text(), 'commissionSplitConfig')]");
            if (scriptNodes == null || scriptNodes.Count == 0)
                return null;

            var scriptContent = scriptNodes[0].InnerText;

            // Extract entire commissionSplitConfig object to get both courtage and expose
            var configMatch = System.Text.RegularExpressions.Regex.Match(
                scriptContent,
                @"commissionSplitConfig:\s*(\{(?:[^{}]|(?:\{(?:[^{}]|(?:\{[^{}]*\}))*\}))*\})",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (!configMatch.Success)
                return null;

            var configJson = configMatch.Groups[1].Value;

            // Remove JavaScript-style properties and convert to valid JSON
            configJson = System.Text.RegularExpressions.Regex.Replace(configJson, @"(\w+):", "\"$1\":");
            configJson = configJson.Replace("'", "\"");
            // Convert JavaScript undefined to JSON null
            configJson = System.Text.RegularExpressions.Regex.Replace(configJson, @"\bundefined\b", "null");
            // Remove trailing commas before } or ]
            configJson = System.Text.RegularExpressions.Regex.Replace(configJson, @",(\s*[}\]])", "$1");

            using var jsonDoc = JsonDocument.Parse(configJson);
            var config = jsonDoc.RootElement;

            // Extract courtage at config level
            var courtageStr = GetJsonString(config, "courtage");
            var commissionPercent = ParseCommission(courtageStr);

            // Extract expose sub-object
            if (!config.TryGetProperty("expose", out var exposeObj) || exposeObj.ValueKind != JsonValueKind.Object)
                return new ExposeMetadata { CommissionPercent = commissionPercent };

            var metadata = new ExposeMetadata
            {
                Address = GetJsonString(exposeObj, "address"),
                PostalCode = GetJsonString(exposeObj, "zip"),
                District = GetJsonString(exposeObj, "location"),
                LivingAreaSqm = ParseInt(GetJsonString(exposeObj, "livingSpace")),
                RoomsCount = GetJsonInt(exposeObj, "noRooms"),
                PriceEur = ParsePrice(GetJsonString(exposeObj, "price")),
                CommissionPercent = commissionPercent,
            };

            return metadata;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error extracting expose metadata: {ex.Message}");
            return null;
        }
    }

    private string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return null;
    }

    private int? GetJsonInt(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }
        return null;
    }

    private int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var digits = System.Text.RegularExpressions.Regex.Replace(value, @"[^\d]", "");
        return int.TryParse(digits, out var result) ? result : null;
    }

    private int? ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var digits = System.Text.RegularExpressions.Regex.Replace(value, @"[^\d]", "");
        return int.TryParse(digits, out var result) ? result : null;
    }

    private double? ParseCommission(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var match = System.Text.RegularExpressions.Regex.Match(value, @"(\d+[.,]\d+)");
        if (!match.Success)
            return null;
        var numStr = match.Groups[1].Value.Replace(",", ".");
        return double.TryParse(numStr, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    /// <summary>
    /// Metadata extracted from the expose JSON embedded in the detail page.
    /// Maps to: address, postalCode, district, livingAreaSqm, roomsCount, priceEur, commissionPercent in Flat model.
    /// </summary>
    public class ExposeMetadata
    {
        public string? Address { get; set; }
        public string? PostalCode { get; set; }
        public string? District { get; set; }
        public int? LivingAreaSqm { get; set; }
        public int? RoomsCount { get; set; }
        public int? PriceEur { get; set; }
        public double? CommissionPercent { get; set; }
    }
}
