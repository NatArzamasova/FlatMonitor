namespace FlatMonitor.Tests.Immowelt;

using NUnit.Framework;

/// <summary>
/// The saved immowelt pages (2026-11-02), cut down to the data scripts the parsers read: <c>list-page.html</c> is
/// results page 2 of the Karlsruhe search, <c>expose-page.html</c> the expose of listing 25DJIC4KVE5U.
/// </summary>
internal static class ImmoweltFixtures
{
    public static string Path(string filename) =>
        System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Immowelt", filename);

    public static string Read(string filename) => File.ReadAllText(Path(filename));
}
