namespace FlatMonitor.Tests.ImmobilienScout24;

using FlatMonitor.Scraping.ImmobilienScout24;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmobilienScout24DebugPageTests
{
    private const string Html = "<html><body>Ich bin kein Roboter</body></html>";

    private string _dir = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"ImmobilienScout24DebugPageTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    [Test]
    public async Task SaveAsync_WhenSavingIsOff_SavesNothingAndSaysHowToTurnItOn()
    {
        var result = await ImmobilienScout24DebugPage.SaveAsync(new ImmobilienScout24Options { SaveDebugPages = false }, Html, default, _dir);

        result.Should().Be("set Scrapers:ImmobilienScout24:SaveDebugPages to true to save the page");
        Directory.GetFiles(_dir).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_WhenSavingIsOn_SavesThePageUnderAUtcTimestampAndNamesTheFile()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await ImmobilienScout24DebugPage.SaveAsync(new ImmobilienScout24Options { SaveDebugPages = true }, Html, default, _dir);

        var file = Directory.GetFiles(_dir).Should().ContainSingle().Subject;
        result.Should().Be($"page ({Html.Length} bytes) saved to {file}");
        (await File.ReadAllTextAsync(file)).Should().Be(Html);
        var stamp = DateTime.ParseExact(Path.GetFileNameWithoutExtension(file)["immobilienscout24_debug_".Length..],
            "yyyyMMdd_HHmmss", null);
        stamp.Should().BeOnOrAfter(before.AddTicks(-(before.Ticks % TimeSpan.TicksPerSecond))).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Test]
    public void ListingCardSelectors_DescribeTheSameStrictCards()
    {
        // One XPath (HtmlAgilityPack) and one CSS selector (Playwright); a loose one would let the robot guard pass
        ImmobilienScout24Selectors.ListingCardXPath.Should().Contain("'listing-card'").And.Contain("@data-obid");
        ImmobilienScout24Selectors.ListingCardCss.Should().Be("div[class*='listing-card'][data-obid]");
    }
}
