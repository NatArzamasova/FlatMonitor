namespace workInProgress.Scraping.OhneMakler;

using FlatMonitor.Scraping.Common;
using HtmlAgilityPack;
using workInProgress.Models;

/// <summary>
/// Parses detail fields from ohne-makler.net detail pages.
/// Delegates selector definitions to OhneMaklerDetailSelectors for easy maintenance.
/// </summary>
public sealed class OhneMaklerDetailPageParser : IDetailParser
{
    private readonly OhneMaklerDetailSelectors _selectors;

    public OhneMaklerDetailPageParser()
    {
        _selectors = new OhneMaklerDetailSelectors();
    }

    public FlatDetails? ParseDetailFields(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (!DetailExtractors.HasValidStructure(doc, _selectors.ValidStructurePatterns))
                return null;

            var title = DetailExtractors.ExtractText(doc, _selectors.TitleXPath);
            var details = new FlatDetails
            {
                Title = title,
                PropertyType = DetailExtractors.ExtractText(doc, _selectors.PropertyTypeXPath),
                BuildYear = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.BuildYearXPath)),
                Floor = DetailExtractors.ExtractText(doc, _selectors.FloorXPath)
                    ?? DetailExtractors.ExtractText(doc, _selectors.FloorAlternate1XPath)
                    ?? DetailExtractors.ExtractText(doc, _selectors.FloorAlternate2XPath),
                Condition = DetailExtractors.ExtractText(doc, _selectors.ConditionXPath),
                Heating = DetailExtractors.ExtractText(doc, _selectors.HeatingXPath),
                ServiceChargeEur = DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.ServiceChargeHausgeldXPath))
                    ?? DetailExtractors.ExtractIntField(DetailExtractors.ExtractText(doc, _selectors.ServiceChargeNebenkostenXPath)),
                EnergyCertificateType = DetailExtractors.ExtractText(doc, _selectors.EnergyCertificateTypeXPath),
                PrimaryEnergySource = DetailExtractors.ExtractText(doc, _selectors.PrimaryEnergySourceXPath),
                EnergyEfficiencyClass = DetailExtractors.ExtractText(doc, _selectors.EnergyEfficiencyClassXPath),
                EnergyConsumption = DetailExtractors.ExtractText(doc, _selectors.EnergyConsumptionXPath),
                ListingStatus = ExtractListingStatus(doc),
                CurrentUse = ExtractCurrentUse(doc),
                Availability = DetailExtractors.ExtractText(doc, _selectors.AvailabilityXPath)
                    ?? DetailExtractors.ExtractText(doc, _selectors.AvailabilityAlternateXPath),
                CommissionPercent = 0,
            };

            return details;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse detail page: {ex.Message}");
            return null;
        }
    }

    public int? ExtractPrice(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var priceText = DetailExtractors.ExtractText(doc, _selectors.PriceXPath);
            return DetailExtractors.ExtractIntField(priceText);
        }
        catch
        {
            return null;
        }
    }

    private string? ExtractListingStatus(HtmlDocument doc)
    {
        var htmlText = doc.DocumentNode.InnerText;
        if (htmlText.Contains("Privatangebot", StringComparison.OrdinalIgnoreCase))
            return "Privatangebot";
        if (htmlText.Contains("Gewerbliches Angebot", StringComparison.OrdinalIgnoreCase))
            return "Gewerbliches Angebot";
        return null;
    }

    private string? ExtractCurrentUse(HtmlDocument doc)
        => DetailExtractors.ExtractFieldByMultiplePatterns(doc, "Aktuelle Nutzung",
            $"//tr[td/div[normalize-space()='Aktuelle Nutzung']]/td[2]/div",
            $"//span[normalize-space()='Aktuelle Nutzung']/preceding-sibling::span[@class]",
            $"//div[normalize-space()='Aktuelle Nutzung']/following-sibling::div")
        ?? DetailExtractors.ExtractFieldByMultiplePatterns(doc, "Nutzung",
            $"//tr[td/div[normalize-space()='Nutzung']]/td[2]/div",
            $"//span[normalize-space()='Nutzung']/preceding-sibling::span[@class]",
            $"//div[normalize-space()='Nutzung']/following-sibling::div");
}
