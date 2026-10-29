namespace FlatMonitor.Scraping.ImmobilienScout24;

using System.Text.RegularExpressions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using HtmlAgilityPack;

/// <summary>Everything the scraper takes from an ImmobilienScout24 expose page; <c>null</c> where the page has no value.</summary>
public sealed record ImmobilienScout24DetailPage(
    string? Title,
    ImmobilienScout24DetailParser.ExposeMetadata? Metadata,
    int? OriginalPrice,
    FlatDetails? Details)
{
    /// <summary>
    /// The card enriched with this page. Expose metadata replaces the card's values; where the page has none
    /// (no metadata script: robot guard, changed markup), the card's values are kept.
    /// </summary>
    public Flat ApplyTo(Flat card)
    {
        var priceEur = Metadata?.PriceEur ?? card.PriceEur;
        return card with
        {
            Title = Title ?? card.Title,
            Address = Metadata?.Address ?? card.Address,
            PostalCode = Metadata?.PostalCode ?? card.PostalCode,
            District = Metadata?.District ?? card.District,
            PriceEur = priceEur,
            OriginalPriceEur = OriginalPrice ?? Metadata?.OriginalPriceEur ?? priceEur,
            RoomsCount = Metadata?.RoomsCount ?? card.RoomsCount,
            LivingAreaSqm = Metadata?.LivingAreaSqm ?? card.LivingAreaSqm,
            CommissionPercent = Metadata?.CommissionPercent ?? card.CommissionPercent,
            Details = Details,
        };
    }
}

/// <summary>
/// Parses ImmobilienScout24 expose pages, loading each page's HTML once.
/// </summary>
public partial class ImmobilienScout24DetailParser
{

    public ImmobilienScout24DetailPage Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return new ImmobilienScout24DetailPage(
            Title: DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.TitleXPath),
            Metadata: ExtractExposeMetadata(doc),
            OriginalPrice: ExtractOriginalPrice(doc),
            Details: ParseDetailFields(doc));
    }

    public FlatDetails? ParseDetailFields(string html) => Parse(html).Details;

    private FlatDetails? ParseDetailFields(HtmlDocument doc)
    {
        var propertyType = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.PropertyTypeXPath);
        var buildYear = GermanNumberParser.ParseInteger(DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.BuildYearXPath));
        var floor = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.FloorXPath);
        var heating = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.HeatingXPath);
        var energyCertType = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.EnergyCertificateTypeXPath);
        var energyConsumption = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.EnergyConsumptionXPath);
        var energyEffClass = DetailExtractors.ExtractAttribute(doc, ImmobilienScout24DetailSelectors.EnergyEfficiencyClassXPath, ImmobilienScout24DetailSelectors.EnergyEfficiencyClassAttributeName);
        var primaryEnergy = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.PrimaryEnergySourceXPath);
        var serviceCharge = GermanNumberParser.ParseInteger(DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.ServiceChargeXPath));
        var availability = DetailExtractors.ExtractText(doc, ImmobilienScout24DetailSelectors.AvailabilityXPath);

        // The page only says when a flat is rented (the "Vermietet" tag, or "vermietet" as availability);
        // without either, whether it is rented is unknown, so CurrentUse stays empty
        var isRented = doc.DocumentNode.SelectSingleNode(ImmobilienScout24DetailSelectors.RentedLabelXPath) != null
            || (availability != null && Rented().IsMatch(availability));
        string? currentUse = isRented ? "Vermietet" : null;

        var details = new FlatDetails
        {
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
        };

        return details;
    }

    private int? ExtractOriginalPrice(HtmlDocument doc)
    {
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
                    var price = GermanNumberParser.ParseInteger(priceText);
                    if (price.HasValue)
                        return price;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extract listing metadata using regex extraction to avoid JSON parsing issues with unescaped quotes in string values.
    /// The page contains a script tag with commissionSplitConfig object containing courtage and expose sub-object.
    /// </summary>
    private ExposeMetadata? ExtractExposeMetadata(HtmlDocument doc)
    {
        var scriptNodes = doc.DocumentNode.SelectNodes("//script[contains(text(), 'commissionSplitConfig')]");
        if (scriptNodes == null || scriptNodes.Count == 0)
            return null;

        var scriptContent = scriptNodes[0].InnerText;

        // Courtage text such as "3,57% inkl. MwSt." (handles both single and double quoted strings)
        var courtageMatch = Courtage().Match(scriptContent);
        var commissionPercent = courtageMatch.Success ? GermanNumberParser.ParseDecimal(courtageMatch.Groups[1].Value) : null;

        var exposeMatch = ExposeObject().Match(scriptContent);
        if (!exposeMatch.Success)
            return new ExposeMetadata { CommissionPercent = commissionPercent };

        var properties = ExtractProperties(exposeMatch.Groups[1].Value);

        var metadata = new ExposeMetadata
        {
            Address = properties.GetValueOrDefault("address"),
            PostalCode = properties.GetValueOrDefault("zip"),
            District = properties.GetValueOrDefault("location"),
            LivingAreaSqm = GermanNumberParser.ParseDecimal(properties.GetValueOrDefault("livingSpace")),
            RoomsCount = GermanNumberParser.ParseDecimal(properties.GetValueOrDefault("noRooms")),
            PriceEur = GermanNumberParser.ParseInteger(properties.GetValueOrDefault("price")),
            CommissionPercent = commissionPercent,
        };

        return metadata;
    }

    /// <summary>
    /// The expose properties the scraper uses, by name; the first value wins if a property repeats.
    /// Keys may be quoted or not, values are quoted strings or plain numbers.
    /// </summary>
    private static Dictionary<string, string> ExtractProperties(string exposeContent)
    {
        var properties = new Dictionary<string, string>();
        foreach (Match match in ExposeProperty().Matches(exposeContent))
            properties.TryAdd(match.Groups["key"].Value, match.Groups["value"].Value);
        return properties;
    }

    // "vermietet / nach Vereinbarung", but not "unvermietet" or "nicht vermietet"
    [GeneratedRegex(@"(?<!nicht\s+)\bvermietet\b", RegexOptions.IgnoreCase)]
    private static partial Regex Rented();

    [GeneratedRegex(@"courtage\s*:\s*['""]([^""]*)['""]")]
    private static partial Regex Courtage();

    [GeneratedRegex(@"expose\s*:\s*\{([^}]+)\}", RegexOptions.Singleline)]
    private static partial Regex ExposeObject();

    [GeneratedRegex(@"[""']?(?<key>address|zip|location|livingSpace|noRooms|price)[""']?\s*:\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>\d+(?:\.\d+)?))")]
    private static partial Regex ExposeProperty();

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
