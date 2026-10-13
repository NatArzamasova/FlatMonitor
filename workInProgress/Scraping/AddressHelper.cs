namespace workInProgress.Scraping;

public static class AddressHelper
{
    public static string SanitizeAddress(string address)
    {
        // Remove trailing " &ndash; Baden-Württemberg" (or similar state suffix)
        var trimmed = address.Replace(" &ndash; Baden-Württemberg", "")
            .Replace(" – Baden-Württemberg", "")
            .Trim();
        return trimmed;
    }
}
