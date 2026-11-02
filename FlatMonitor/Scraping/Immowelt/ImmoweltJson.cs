namespace FlatMonitor.Scraping.Immowelt;

using System.Text.Json;

/// <summary>Reading values from immowelt's page data by property path; <c>null</c> where a path leads nowhere.</summary>
internal static class ImmoweltJson
{
    public static JsonElement? Element(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                return null;
        }

        return element;
    }

    /// <summary>The trimmed string at the path; <c>null</c> if it is missing, blank or no string.</summary>
    public static string? Text(JsonElement element, params string[] path) =>
        Element(element, path) is { ValueKind: JsonValueKind.String } value && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    public static double? Number(JsonElement element, params string[] path) =>
        Element(element, path) is { ValueKind: JsonValueKind.Number } value ? value.GetDouble() : null;

    /// <summary>
    /// The elements of the array at the path whose <c>type</c> is <paramref name="type"/>, e.g. a fact or feature.
    /// </summary>
    public static IEnumerable<JsonElement> OfType(JsonElement element, string type, params string[] path) =>
        Element(element, path) is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Where(item => Text(item, "type") == type)
            : [];

    /// <summary>
    /// A <c>location.address</c> object as "Yorckstraße 49, 76135 Karlsruhe"; most listings publish no street, then
    /// "76135 Karlsruhe". <c>null</c> without postal code and city.
    /// </summary>
    public static string? FormatAddress(JsonElement? address)
    {
        if (address is not { ValueKind: JsonValueKind.Object } value)
            return null;

        var place = string.Join(" ", new[] { Text(value, "zipCode"), Text(value, "city") }.OfType<string>());
        var parts = new[] { Text(value, "street"), place.Length > 0 ? place : null }.OfType<string>().ToList();
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
