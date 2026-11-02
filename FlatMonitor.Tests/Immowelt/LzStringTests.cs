namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class LzStringTests
{
    // Compressed with LZString.compressToBase64 of the JavaScript library lz-string 1.5.0
    [TestCase("BIUwNmD2A0AEDukBOYAmQ===", "Hello, world")]
    [TestCase("IY18ZXcQ", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", TestName = "DecompressFromBase64_RepeatedText")]
    [TestCase("N4IgJglgzgLgThAxjEAuEBxOA3mAvCAOwHMBTQkAGhAAcFFS0AWJgTgAZOBfIA==", """{"district":"Grötzingen","price":449000}""",
        TestName = "DecompressFromBase64_JsonWithUmlauts")]
    [TestCase("IZA=", "a")]
    public void DecompressFromBase64_ReturnsTheOriginalText(string compressed, string expected)
    {
        LzString.DecompressFromBase64(compressed).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    public void DecompressFromBase64_NoInput_IsEmpty(string? input)
    {
        LzString.DecompressFromBase64(input).Should().BeEmpty();
    }

    [Test]
    public void DecompressFromBase64_CharacterOutsideBase64_IsNull()
    {
        LzString.DecompressFromBase64("BIUwNmD2A0AE!").Should().BeNull();
    }

    [Test]
    public void DecompressFromBase64_CutOffInput_IsNull()
    {
        LzString.DecompressFromBase64("N4IgJglgzgLgThAxjEAuEBxOA3mAvCAO").Should().BeNull();
    }

    [Test]
    public void DecompressFromBase64_ListPageFixture_IsTheSearchJson()
    {
        var html = ImmoweltFixtures.Read("list-page.html");
        var script = ImmoweltPages.ReadDataScript(html, ImmoweltPages.ListDataScript)!;
        var compressed = System.Text.Json.JsonDocument.Parse(script).RootElement
            .GetProperty("data").GetProperty("classified-serp-init-data").GetString();

        var json = LzString.DecompressFromBase64(compressed);

        // Length and start as decompressed by the JavaScript library
        json.Should().StartWith("""{"pageProps":{"searchModel":{"locations":""");
        json!.Length.Should().Be(296360);
    }
}
