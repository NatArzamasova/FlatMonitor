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

    public FlatDetails? ParseDetailFields(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var root = doc.DocumentNode;

            var title = DetailExtractors.ExtractText(doc, _selectors.TitleXPath);
            var address = DetailExtractors.ExtractText(doc, _selectors.AddressXPath);
            var propertyType = DetailExtractors.ExtractText(doc, _selectors.PropertyTypeXPath);
            var buildYear = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.BuildYearXPath));
            var floor = DetailExtractors.ExtractText(doc, _selectors.FloorXPath);
            var heating = DetailExtractors.ExtractText(doc, _selectors.HeatingXPath);
            var energyCertType = DetailExtractors.ExtractText(doc, _selectors.EnergyCertificateTypeXPath);
            var energyConsumption = DetailExtractors.ExtractText(doc, _selectors.EnergyConsumptionXPath);
            var energyEffClass = DetailExtractors.ExtractAttribute(doc, _selectors.EnergyEfficiencyClassXPath, _selectors.EnergyEfficiencyClassAttributeName);
            var primaryEnergy = DetailExtractors.ExtractText(doc, _selectors.PrimaryEnergySourceXPath);
            var serviceCharge = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.ServiceChargeXPath));
            var availability = DetailExtractors.ExtractText(doc, _selectors.AvailabilityXPath);

            // Map Availability to CurrentUse
            string? currentUse = null;
            if (!string.IsNullOrWhiteSpace(availability))
            {
                if (availability.Contains("Vermitet", StringComparison.OrdinalIgnoreCase))
                    currentUse = "Vermitet";
                else if (availability.Contains("Sofort", StringComparison.OrdinalIgnoreCase))
                    currentUse = "Nicht vermietet";
            }

            // Extract commission percent from expose metadata
            var metadata = ExtractExposeMetadata(html);
            var commissionPercent = metadata?.CommissionPercent;

            var details = new FlatDetails
            {
                Title = title,
                PropertyType = propertyType,
                BuildYear = buildYear,
                Floor = floor,
                Heating = heating,
                EnergyCertificateType = energyCertType,
                EnergyConsumption = energyConsumption,
                EnergyEfficiencyClass = energyEffClass,
                PrimaryEnergySource = primaryEnergy,
                ServiceChargeEur = serviceCharge,
                Availability = availability,
                CurrentUse = currentUse,
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

    public int? ExtractOriginalPrice(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Look for <s> tag (HTML semantic for strikethrough)
            var struckNodes = doc.DocumentNode.SelectNodes("//s");

            // Fallback to span with font-strike class
            if (struckNodes == null || struckNodes.Count == 0)
            {
                struckNodes = doc.DocumentNode.SelectNodes("//span[@class='font-strike']");
            }

            // Fallback to span containing font-strike class
            if (struckNodes == null || struckNodes.Count == 0)
            {
                struckNodes = doc.DocumentNode.SelectNodes("//span[contains(@class, 'font-strike')]");
            }

            if (struckNodes != null && struckNodes.Count > 0)
            {
                foreach (var node in struckNodes)
                {
                    var priceText = node.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(priceText) && priceText.Contains("€"))
                    {
                        var price = ParsePrice(priceText);
                        if (price.HasValue)
                            return price;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error extracting original price: {ex.Message}");
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
    /// Extract listing metadata using regex extraction to avoid JSON parsing issues with unescaped quotes in string values.
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

            // Extract courtage value using regex (handles both single and double quoted strings)
            var courtageMatch = System.Text.RegularExpressions.Regex.Match(scriptContent, @"courtage\s*:\s*['""]([^""]*)['""]");
            var courtageStr = courtageMatch.Success ? courtageMatch.Groups[1].Value : null;
            var commissionPercent = ParseCommission(courtageStr);

            // Extract expose object and its properties using regex
            var exposeMatch = System.Text.RegularExpressions.Regex.Match(scriptContent, @"expose\s*:\s*\{([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!exposeMatch.Success)
                return new ExposeMetadata { CommissionPercent = commissionPercent };

            var exposeContent = exposeMatch.Groups[1].Value;

            // Extract individual expose properties (handles both quoted and unquoted keys)
            var address = ExtractPropertyValue(exposeContent, "address");
            var zip = ExtractPropertyValue(exposeContent, "zip");
            var location = ExtractPropertyValue(exposeContent, "location");
            var livingSpace = ExtractPropertyValue(exposeContent, "livingSpace");
            var noRooms = ExtractPropertyValue(exposeContent, "noRooms");
            var price = ExtractPropertyValue(exposeContent, "price");

            var metadata = new ExposeMetadata
            {
                Address = address,
                PostalCode = zip,
                District = location,
                LivingAreaSqm = ParseDouble(livingSpace),
                RoomsCount = string.IsNullOrWhiteSpace(noRooms) ? null : double.TryParse(noRooms, System.Globalization.CultureInfo.InvariantCulture, out var rooms) ? rooms : null,
                PriceEur = ParsePrice(price),
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

    private string? ExtractPropertyValue(string content, string propertyName)
    {
        // Match: "propertyName": "value" or 'propertyName': 'value' or propertyName: "value" or propertyName: value
        var pattern = $@"[""']?{propertyName}[""']?\s*:\s*(?:""([^""]*)""|'([^']*)'|(\d+(?:\.\d+)?))";
        var match = System.Text.RegularExpressions.Regex.Match(content, pattern);
        if (!match.Success)
            return null;

        // Check which group actually matched (not just has a value, but was actually captured)
        if (match.Groups[1].Success)
            return match.Groups[1].Value;
        if (match.Groups[2].Success)
            return match.Groups[2].Value;
        if (match.Groups[3].Success)
            return match.Groups[3].Value;

        return null;
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

    private double? GetJsonDouble(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetDouble();
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

    private double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = System.Text.RegularExpressions.Regex.Replace(value, @"[^\d.,]", "");
        normalized = normalized.Replace(",", ".");
        return double.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
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
        public double? LivingAreaSqm { get; set; }
        public double? RoomsCount { get; set; }
        public int? PriceEur { get; set; }
        public int? OriginalPriceEur { get; set; }
        public double? CommissionPercent { get; set; }
    }
}
