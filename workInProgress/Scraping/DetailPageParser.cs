namespace workInProgress.Scraping;

using HtmlAgilityPack;
using workInProgress.Models;

public sealed class DetailPageParser
{
    public Details? ParseDetailPage(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Check if HTML is completely empty/malformed by looking for any table or key-facts structure
            var hasTableStructure = doc.DocumentNode.SelectNodes("//tr") != null &&
                doc.DocumentNode.SelectNodes("//tr").Count > 0;
            var hasKeyFactsStructure = doc.DocumentNode.SelectNodes("//span[@class]") != null &&
                doc.DocumentNode.SelectNodes("//span[@class]").Count > 0;
            var hasDivStructure = doc.DocumentNode.SelectNodes("//div[contains(text(), 'Heizung') or contains(text(), 'Verfügbarkeit')]") != null &&
                doc.DocumentNode.SelectNodes("//div[contains(text(), 'Heizung') or contains(text(), 'Verfügbarkeit')]").Count > 0;

            if (!hasTableStructure && !hasKeyFactsStructure && !hasDivStructure)
                return null;

            var details = new Details
            {
                PropertyType = ExtractField(doc, "Objektart"),
                BuildYear = ExtractIntField(doc, "Baujahr"),
                Floor = ExtractField(doc, "Etage") ?? ExtractField(doc, "Geschoss"),
                Condition = ExtractField(doc, "Zustand"),
                Heating = ExtractField(doc, "Heizung"),
                Flooring = ExtractField(doc, "Bodenbelag"),
                ServiceChargeEur = ExtractIntField(doc, "Nebenkosten"),
                EnergyCertificateType = ExtractField(doc, "Energieausweistyp"),
                PrimaryEnergySource = ExtractField(doc, "Wesentlicher Energieträger"),
                ContactName = ExtractContactName(doc),
                ListingStatus = ExtractListingStatus(doc),
                CurrentUse = ExtractCurrentUse(doc),
                Availability = ExtractField(doc, "Verfügbarkeit"),
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
    {
        // Pattern 1: Table row with label in first <td>, value in second <td>
        var tableMatch = doc.DocumentNode.SelectNodes(
            $"//tr[td/div[contains(text(), '{label}')]]/td[2]/div");
        if (tableMatch != null && tableMatch.Count > 0)
        {
            var text = tableMatch[0].InnerText?.Trim();
            if (!string.IsNullOrEmpty(text))
                return text;
        }

        // Pattern 2: Key facts block - value in one span, label in sibling span
        var keyFactsNodes = doc.DocumentNode.SelectNodes(
            $"//span[contains(text(), '{label}')]/preceding-sibling::span[@class]");
        if (keyFactsNodes != null && keyFactsNodes.Count > 0)
        {
            var text = keyFactsNodes[0].InnerText?.Trim();
            if (!string.IsNullOrEmpty(text))
                return text;
        }

        // Pattern 3: Div with label text followed by sibling div with value-text class
        var divWithLabel = doc.DocumentNode.SelectSingleNode(
            $"//div[contains(text(), '{label}')]/following-sibling::div");
        if (divWithLabel != null)
        {
            var text = divWithLabel.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text))
                return text;
        }

        return null;
    }

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

    // Contact name can appear as plain text in the page (often near a contact section or form).
    private static string? ExtractContactName(HtmlDocument doc)
    {
        // Look for a name in common contact sections
        var contactSection = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'contact') or contains(text(), 'Kontakt')]");
        if (contactSection != null)
        {
            // Try to find a name within the contact section
            var nameNode = contactSection.SelectSingleNode(".//p | .//div[@class]");
            if (nameNode != null)
            {
                var text = nameNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length < 100) // Rough filter
                    return text;
            }
        }

        // Fallback: look for "Ansprechpartner" label in the details table
        return ExtractField(doc, "Ansprechpartner");
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
        var htmlText = doc.DocumentNode.InnerText;
        if (htmlText.Contains("Vermietet", StringComparison.OrdinalIgnoreCase))
            return "Vermietet";
        if (htmlText.Contains("Eigennutzung", StringComparison.OrdinalIgnoreCase))
            return "Eigennutzung";
        // Look for explicit field
        return ExtractField(doc, "Nutzung") ?? ExtractField(doc, "Aktuelle Nutzung");
    }
}
