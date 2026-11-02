namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmoweltDebugPageTests
{
    private const string Html = "<html><body>captcha-delivery.com</body></html>";

    private string _dir = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"ImmoweltDebugPageTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public async Task SaveAsync_WhenSavingIsOff_SavesNothingAndSaysHowToTurnItOn()
    {
        var result = await ImmoweltDebugPage.SaveAsync(new ImmoweltOptions { SaveDebugPages = false }, Html, "page1", default, _dir);

        result.Should().Be("set Scrapers:Immowelt:SaveDebugPages to true to save the page");
        Directory.GetFiles(_dir).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_WhenSavingIsOn_SavesThePageUnderItsNameAndNamesTheFile()
    {
        var result = await ImmoweltDebugPage.SaveAsync(new ImmoweltOptions { SaveDebugPages = true }, Html, "page3", default, _dir);

        var file = Directory.GetFiles(_dir).Should().ContainSingle().Subject;
        Path.GetFileName(file).Should().StartWith("immowelt_debug_page3_").And.EndWith(".html");
        (await File.ReadAllTextAsync(file)).Should().Be(Html);
        result.Should().Be($"page ({Html.Length} bytes) saved to {file}");
    }
}
