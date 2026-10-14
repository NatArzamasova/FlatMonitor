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

            var title = DetailExtractors.ExtractText(doc, _selectors.TitleXPath);
            var address = DetailExtractors.ExtractText(doc, _selectors.AddressXPath);
            var propertyType = DetailExtractors.ExtractText(doc, _selectors.PropertyTypeXPath);
            var buildYear = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.BuildYearXPath));
            var heating = DetailExtractors.ExtractText(doc, _selectors.HeatingXPath);
            var energyCertType = DetailExtractors.ExtractText(doc, _selectors.EnergyCertificateTypeXPath);
            var energyConsumption = DetailExtractors.ExtractText(doc, _selectors.EnergyConsumptionXPath);
            var energyEffClass = DetailExtractors.ExtractAttribute(doc, _selectors.EnergyEfficiencyClassXPath, _selectors.EnergyEfficiencyClassAttributeName);
            var primaryEnergy = DetailExtractors.ExtractText(doc, _selectors.PrimaryEnergySourceXPath);
            var serviceCharge = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.ServiceChargeXPath));

            // Extract commission percent from expose metadata
            var metadata = ExtractExposeMetadata(html);
            var commissionPercent = metadata?.CommissionPercent;

            var details = new Details
            {
                Title = title,
                PropertyType = propertyType,
                BuildYear = buildYear,
                Heating = heating,
                EnergyCertificateType = energyCertType,
                EnergyConsumption = energyConsumption,
                EnergyEfficiencyClass = energyEffClass,
                PrimaryEnergySource = primaryEnergy,
                ServiceChargeEur = serviceCharge,
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
            // Use a more robust approach: find commissionSplitConfig: { and match braces carefully
            var startIdx = scriptContent.IndexOf("commissionSplitConfig:");
            if (startIdx == -1)
                return null;

            var openBraceIdx = scriptContent.IndexOf('{', startIdx);
            if (openBraceIdx == -1)
                return null;

            // Count braces to find the matching closing brace, accounting for string delimiters
            int braceCount = 0;
            int endIdx = -1;
            bool inBraceString = false;
            char braceStringDelimiter = '\0';
            bool braceEscaped = false;

            for (int i = openBraceIdx; i < scriptContent.Length; i++)
            {
                char c = scriptContent[i];

                if (braceEscaped)
                {
                    braceEscaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    braceEscaped = true;
                    continue;
                }

                if ((c == '"' || c == '\'') && !inBraceString)
                {
                    inBraceString = true;
                    braceStringDelimiter = c;
                    continue;
                }
                if (inBraceString && c == braceStringDelimiter)
                {
                    inBraceString = false;
                    braceStringDelimiter = '\0';
                    continue;
                }

                if (!inBraceString)
                {
                    if (c == '{')
                        braceCount++;
                    else if (c == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            endIdx = i;
                            break;
                        }
                    }
                }
            }

            if (endIdx == -1)
                return null;

            var configJson = scriptContent.Substring(openBraceIdx, endIdx - openBraceIdx + 1);

            // Escape inner quotes and newlines in string values before converting quotes
            var sb = new System.Text.StringBuilder();
            bool inJsonString = false;
            char jsonStringDelimiter = '\0';
            bool jsonEscaped = false;
            for (int i = 0; i < configJson.Length; i++)
            {
                char c = configJson[i];
                if (jsonEscaped)
                {
                    sb.Append(c);
                    jsonEscaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    sb.Append(c);
                    jsonEscaped = true;
                    continue;
                }
                if ((c == '"' || c == '\'') && !inJsonString)
                {
                    inJsonString = true;
                    jsonStringDelimiter = c;
                    sb.Append(c);
                    continue;
                }
                if (inJsonString && c == jsonStringDelimiter)
                {
                    inJsonString = false;
                    jsonStringDelimiter = '\0';
                    sb.Append(c);
                    continue;
                }
                if (inJsonString && c == '\n')
                {
                    sb.Append("\\n");
                    continue;
                }
                if (inJsonString && c == '\r')
                {
                    sb.Append("\\r");
                    continue;
                }
                // Escape quotes that appear inside strings
                if (inJsonString && c == '"' && jsonStringDelimiter == '"')
                {
                    sb.Append("\\\"");
                    continue;
                }
                if (inJsonString && c == '"' && jsonStringDelimiter == '\'')
                {
                    sb.Append("\\\"");
                    continue;
                }
                if (inJsonString && c == '\'' && jsonStringDelimiter == '"')
                {
                    sb.Append("\\'");
                    continue;
                }
                sb.Append(c);
            }
            configJson = sb.ToString();

            // Remove JavaScript-style properties and convert to valid JSON
            configJson = System.Text.RegularExpressions.Regex.Replace(configJson, @"(\w+):", "\"$1\":");
            configJson = configJson.Replace("'", "\"");
            // Convert JavaScript undefined to JSON null
            configJson = System.Text.RegularExpressions.Regex.Replace(configJson, @"\bundefined\b", "null");
            // Remove trailing commas before } or ]
            configJson = System.Text.RegularExpressions.Regex.Replace(configJson, @",(\s*[}\]])", "$1");

            JsonDocument jsonDoc;
            try
            {
                jsonDoc = JsonDocument.Parse(configJson);
            }
            catch (Exception parseEx)
            {
                Console.Error.WriteLine($"JSON parsing error: {parseEx.Message}");
                Console.Error.WriteLine($"JSON (first 1000 chars): {configJson.Substring(0, Math.Min(1000, configJson.Length))}");
                Console.Error.WriteLine($"JSON length: {configJson.Length}");
                throw;
            }

            using (jsonDoc)
            {
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
                    LivingAreaSqm = ParseDouble(GetJsonString(exposeObj, "livingSpace")),
                    RoomsCount = GetJsonDouble(exposeObj, "noRooms"),
                    PriceEur = ParsePrice(GetJsonString(exposeObj, "price")),
                    CommissionPercent = commissionPercent,
                };

                return metadata;
            }
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
        public double? CommissionPercent { get; set; }
    }
}
