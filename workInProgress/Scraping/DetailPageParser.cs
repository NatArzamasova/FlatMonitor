namespace workInProgress.Scraping;

using HtmlAgilityPack;
using workInProgress.Models;

public sealed class DetailPageParser
{
    private static bool HasNodes(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectNodes(xpath)?.Count > 0;

    private static string? ExtractText(HtmlNode? node)
        => node?.InnerText?.Trim() is { Length: > 0 } text ? text : null;

    private static string? ExtractFieldByMultiplePatterns(HtmlDocument doc, string label, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes != null && nodes.Count > 0)
            {
                var text = ExtractText(nodes[0]);
                if (text != null)
                    return text;
            }
        }
        return null;
    }

    private static bool HasValidStructure(HtmlDocument doc)
        => HasNodes(doc, "//tr")
        || HasNodes(doc, "//span[@class]")
        || HasNodes(doc, "//div[contains(text(), 'Heizung') or contains(text(), 'Verfügbarkeit')]");

    public Details? ParseDetailPage(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (!HasValidStructure(doc))
                return null;

            var details = new Details
            {
                PropertyType = ExtractField(doc, "Objektart"),
                BuildYear = ExtractIntField(doc, "Baujahr"),
                Floor = ExtractField(doc, "Etage") ?? ExtractField(doc, "Geschoss") ?? ExtractField(doc, "Stockwerk"),
                Condition = ExtractField(doc, "Zustand"),
                Heating = ExtractField(doc, "Heizung"),
                ServiceChargeEur = ExtractIntField(doc, "Hausgeld mtl.") ?? ExtractIntField(doc, "Nebenkosten"),
                EnergyCertificateType = ExtractField(doc, "Energieausweistyp"),
                PrimaryEnergySource = ExtractField(doc, "Wesentlicher Energieträger"),
                EnergyEfficiencyClass = ExtractField(doc, "Energieeffizienzklasse"),
                EnergyConsumption = ExtractField(doc, "Endenergieverbrauch"),
                ListingStatus = ExtractListingStatus(doc),
                CurrentUse = ExtractCurrentUse(doc),
                Availability = ExtractField(doc, "Verfügbarkeit") ?? ExtractField(doc, "Übernahme ab"),
            };

            return details;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse detail page: {ex.Message}");
            return null;
        }
    }

    // Extract a string field by looking for a label and its associated value.
    // Three patterns:
    // 1. Label in <td> followed by value in next <td>
    // 2. Value <span> followed by label <span> (key facts pattern)
    // 3. Label <div> followed by value <div> (generic div pattern)
    private static string? ExtractField(HtmlDocument doc, string label)
        => ExtractFieldByMultiplePatterns(doc, label,
            $"//tr[td/div[normalize-space()='{label}']]/td[2]/div",
            $"//span[normalize-space()='{label}']/preceding-sibling::span[@class]",
            $"//div[normalize-space()='{label}']/following-sibling::div");

    // Extract an integer field (e.g., Baujahr, Nebenkosten).
    private static int? ExtractIntField(HtmlDocument doc, string label)
    {
        var value = ExtractField(doc, label);
        if (string.IsNullOrEmpty(value))
            return null;

        // Remove non-digit characters and parse
        var digits = System.Text.RegularExpressions.Regex.Replace(value, @"[^\d]", "");
        if (int.TryParse(digits, out int result))
            return result;

        return null;
    }

    // Listing status: "Privatangebot" or "Gewerbliches Angebot"
    private static string? ExtractListingStatus(HtmlDocument doc)
    {
        var htmlText = doc.DocumentNode.InnerText;
        if (htmlText.Contains("Privatangebot", StringComparison.OrdinalIgnoreCase))
            return "Privatangebot";
        if (htmlText.Contains("Gewerbliches Angebot", StringComparison.OrdinalIgnoreCase))
            return "Gewerbliches Angebot";
        return null;
    }

    // Current use: "Vermietet" or "Eigennutzung" or similar.
    private static string? ExtractCurrentUse(HtmlDocument doc)
    {
        // Use ExtractField for exact matching to avoid substring collisions
        // (e.g., "Vermietet" matching "Nicht vermietet")
        return ExtractField(doc, "Aktuelle Nutzung") ?? ExtractField(doc, "Nutzung");
    }
}
