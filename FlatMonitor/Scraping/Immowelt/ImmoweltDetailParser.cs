namespace FlatMonitor.Scraping.Immowelt;

using System.Text.Json;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Common;
using static ImmoweltJson;

/// <summary>
/// Everything the scraper takes from an immowelt expose page; <c>null</c> where the page has no value.
/// <paramref name="IsDeactivated"/>: the page says the listing is no longer published.
/// </summary>
public sealed record ImmoweltDetailPage(
    string? Title,
    string? Address,
    string? PostalCode,
    string? District,
    int? PriceEur,
    double? RoomsCount,
    double? LivingAreaSqm,
    double? CommissionPercent,
    FlatDetails? Details,
    bool IsDeactivated = false)
{
    /// <summary>
    /// The card enriched with this page. The page's values replace the card's (the expose is current); rooms and area
    /// are the exception: the card has them exactly (118.09 m²), the page rounded for display ("118,1 m²").
    /// </summary>
    public Flat ApplyTo(Flat card)
    {
        var priceEur = PriceEur ?? card.PriceEur;
        return card with
        {
            Title = Title ?? card.Title,
            Address = Address ?? card.Address,
            PostalCode = PostalCode ?? card.PostalCode,
            District = District ?? card.District,
            PriceEur = priceEur,
            // The card's previous price, if any, is the original one; the expose has none
            OriginalPriceEur = card.OriginalPriceEur is { } original && original != card.PriceEur ? original : priceEur,
            RoomsCount = card.RoomsCount ?? RoomsCount,
            LivingAreaSqm = card.LivingAreaSqm ?? LivingAreaSqm,
            CommissionPercent = CommissionPercent ?? card.CommissionPercent,
            Details = Details,
        };
    }
}

/// <summary>
/// Reads an immowelt expose page from its data script (<see cref="ImmoweltPages.ExposeDataScript"/>):
/// <c>app_demand_referral_cldp.data.classified</c>, whose <c>sections</c> hold price, commission, facts and energy data.
/// </summary>
public sealed class ImmoweltDetailParser
{
    /// <summary>The page's values, or <c>null</c> if the page has no expose data (the DataDome guard, a changed page).</summary>
    public ImmoweltDetailPage? Parse(string html)
    {
        var scriptJson = ImmoweltPages.ReadDataScript(html, ImmoweltPages.ExposeDataScript);
        if (scriptJson == null)
            return null;

        using var script = JsonDocument.Parse(scriptJson);
        if (Element(script.RootElement, "app_demand_referral_cldp", "data", "classified") is not { ValueKind: JsonValueKind.Object } classified)
            return null;

        var address = Element(classified, "sections", "location", "address");
        return new ImmoweltDetailPage(
            Title: Text(classified, "sections", "mainDescription", "headline"),
            Address: FormatAddress(address),
            PostalCode: address is { } a ? Text(a, "zipCode") : null,
            District: address is { } d ? Text(d, "district") : null,
            PriceEur: GermanNumberParser.ParseInteger(Text(classified, "sections", "price", "base", "main", "value", "main", "value")),
            RoomsCount: GermanNumberParser.ParseDecimal(FactValue(classified, "numberOfRooms", "splitValue")),
            LivingAreaSqm: GermanNumberParser.ParseDecimal(FactValue(classified, "livingSpace", "splitValue")),
            CommissionPercent: ParseCommission(Text(classified, "sections", "price", "base", "commissionFee", "value")),
            Details: ParseDetails(classified),
            IsDeactivated: Element(classified, "metadata", "status", "status") is { ValueKind: JsonValueKind.False });
    }

    private static FlatDetails ParseDetails(JsonElement classified)
    {
        var certificate = Element(classified, "sections", "energy", "certificates") is { ValueKind: JsonValueKind.Array } certificates
            && certificates.GetArrayLength() > 0
                ? certificates[0]
                : (JsonElement?)null;
        var scale = certificate is { } c && Element(c, "scales") is { ValueKind: JsonValueKind.Array } scales && scales.GetArrayLength() > 0
            ? scales[0]
            : (JsonElement?)null;

        return new FlatDetails
        {
            PropertyType = PropertyType(Text(classified, "sections", "hardFacts", "title")),
            BuildYear = GermanNumberParser.ParseInteger(EnergyFeature(classified, "yearOfConstruction")),
            Floor = FactValue(classified, "numberOfFloors", "value"),
            Condition = EnergyFeature(classified, "state"),
            Heating = EnergyFeature(classified, "heatingSystem"),
            ServiceChargeEur = GermanNumberParser.ParseInteger(AdditionalPrice(classified, "Hausgeld")),
            EnergyCertificateType = certificate is { } basedOn ? FeatureValue(basedOn, "basedOn") : null,
            PrimaryEnergySource = certificate is { } source ? FeatureValue(source, "primaryEnergySource") : null,
            EnergyEfficiencyClass = scale is { } rating ? Text(rating, "efficiencyClass", "rating") : null,
            EnergyConsumption = scale is { } consumption && Element(consumption, "values") is { ValueKind: JsonValueKind.Array } values
                && values.GetArrayLength() > 0
                    ? Text(values[0], "value")
                    : null,
        };
    }

    /// <summary>"Maisonette zum Kauf" → "Maisonette".</summary>
    private static string? PropertyType(string? hardFactsTitle) =>
        hardFactsTitle?.Replace("zum Kauf", "", StringComparison.OrdinalIgnoreCase).Trim() is { Length: > 0 } type ? type : null;

    /// <summary>"3,57% des Kaufpreises" → 3.57; "provisionsfrei" → 0; no fee on the page → <c>null</c> (unknown).</summary>
    private static double? ParseCommission(string? fee) =>
        fee != null && fee.Contains("provisionsfrei", StringComparison.OrdinalIgnoreCase) ? 0 : GermanNumberParser.ParseDecimal(fee);

    private static string? FactValue(JsonElement classified, string type, string field) =>
        OfType(classified, type, "sections", "hardFacts", "facts").Select(fact => Text(fact, field)).FirstOrDefault(v => v != null);

    private static string? EnergyFeature(JsonElement classified, string type) =>
        Element(classified, "sections", "energy") is { } energy ? FeatureValue(energy, type) : null;

    private static string? FeatureValue(JsonElement parent, string type) =>
        OfType(parent, type, "features").Select(feature => Text(feature, "value")).FirstOrDefault(v => v != null);

    /// <summary>A running cost such as "Hausgeld" from <c>sections.price.additional[].details[]</c>.</summary>
    private static string? AdditionalPrice(JsonElement classified, string label)
    {
        if (Element(classified, "sections", "price", "additional") is not { ValueKind: JsonValueKind.Array } groups)
            return null;

        return groups.EnumerateArray()
            .Select(group => Element(group, "details"))
            .Where(details => details is { ValueKind: JsonValueKind.Array })
            .SelectMany(details => details!.Value.EnumerateArray())
            .Where(detail => Text(detail, "label", "main") == label)
            .Select(detail => Text(detail, "value", "main", "value"))
            .FirstOrDefault(value => value != null);
    }
}
