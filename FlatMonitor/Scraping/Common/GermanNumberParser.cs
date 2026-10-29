namespace FlatMonitor.Scraping.Common;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Reads the first number in a text as written on German listing pages: <c>.</c> groups thousands, <c>,</c> is the
/// decimal separator ("1.250.000 €" → 1250000, "84,5 m²" → 84.5, "1.250,5" → 1250.5). A <c>.</c> not followed by
/// exactly three digits is a decimal point, as in embedded JSON ("104.5" → 104.5).
/// </summary>
public static partial class GermanNumberParser
{
    /// <summary>One number in either form; a grouped number must not run on into a fourth digit.</summary>
    private const string NumberPattern = @"\d{1,3}(?:\.\d{3})+(?!\d)(?:,\d+)?|\d+(?:[.,]\d+)?";

    /// <summary>The first number in the text, or <c>null</c> if it has none.</summary>
    public static double? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Number().Match(text);
        return match.Success ? ToDouble(match.Value) : null;
    }

    /// <summary>The first number in the text without its decimal places ("270.000,50 €" → 270000), or <c>null</c>.</summary>
    public static int? ParseInteger(string? text) =>
        ParseDecimal(text) is { } value && value <= int.MaxValue ? (int)Math.Truncate(value) : null;

    /// <summary>Converts one number already cut out of its text (for example by a pattern with a unit).</summary>
    public static double? ToDouble(string number)
    {
        var invariant = GroupedThousands().IsMatch(number) || number.Contains(',')
            ? number.Replace(".", "").Replace(',', '.')
            : number;

        return double.TryParse(invariant, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    [GeneratedRegex(NumberPattern)]
    private static partial Regex Number();

    [GeneratedRegex(@"^\d{1,3}(?:\.\d{3})+(?:,\d+)?$")]
    private static partial Regex GroupedThousands();
}
