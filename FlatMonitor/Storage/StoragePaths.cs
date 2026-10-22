namespace FlatMonitor.Storage;

public static class StoragePaths
{
    public static string GetDefaultDataDirectory()
    {
        var appDir = AppContext.BaseDirectory;
        var projectRoot = Path.GetFullPath(Path.Combine(appDir, "..", "..", ".."));
        return Path.Combine(projectRoot, "data");
    }
}
