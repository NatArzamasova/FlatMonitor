namespace FlatMonitor.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Scraping.OhneMakler;


[TestFixture]
public class OhneMaklerFlatCardParserTests
{
    private OhneMaklerFlatCardParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new OhneMaklerFlatCardParser();
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "OhneMakler", filename);
    }

    [Test]
    public void ParsePage_WithValidFlats_ReturnsCorrectCount()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
    }

    [Test]
    public void ParsePage_WithValidFlats_ExtractsFlatDetailsCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        var firstFlat = result[0].Flat;
        firstFlat.ListingId.Should().Be("502814");
        firstFlat.Url.Should().Be("https://www.ohne-makler.net/immobilie/502814/");
        firstFlat.DetailPageUrl.Should().Be(firstFlat.Url);
        firstFlat.Title.Should().Contain("Altbauperle");
        firstFlat.PostalCode.Should().Be("76135");
        firstFlat.District.Should().Contain("Weststadt");
        firstFlat.PriceEur.Should().Be(270000);
        firstFlat.RoomsCount.Should().Be(3);
        firstFlat.LivingAreaSqm.Should().Be(82);
    }

    [Test]
    public void ParsePage_WithMissingRoomCount_ReturnsNullForRooms()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        var thirdFlat = result[2].Flat;
        thirdFlat.ListingId.Should().Be("502816");
        thirdFlat.RoomsCount.Should().BeNull();
        thirdFlat.LivingAreaSqm.Should().Be(78);
    }

    [Test]
    public void ParsePage_WithValidListings_ExtractsDetailPageUrls()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Should().AllSatisfy(item =>
        {
            item.DetailPageUrl.Should().NotBeNullOrEmpty();
            item.DetailPageUrl.Should().Contain("/immobilie/");
        });
    }

    [Test]
    public void ParsePage_AlwaysIncludesScrapedAtUtcTimestamp()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Should().AllSatisfy(item =>
        {
            item.Flat.ScrapedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            item.Flat.ScrapedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        });
    }

    [Test]
    public void ParsePage_WithEmptyHtml_ReturnsEmptyList()
    {
        // Arrange
        var html = "<html><body></body></html>";

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public void ParsePage_WithMalformedFlat_SkipsIt()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href=""/immobilie/123/"">Valid Flat</a>
                    <a href=""#"">Invalid Link</a>
                    <a href=""/immobilie/456/"">Another Valid</a>
                </body>
            </html>";

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public void ParsePage_KeepsHalfRoomsAndDecimalArea()
    {
        var html = """
            <div class="card">
              <a href="/immobilie/600001/"><h2>Dachgeschosswohnung</h2></a>
              <span>389.000 €</span>
              <span>2,5 Zimmer</span>
              <span>74,5 m²</span>
              <span>76133 Karlsruhe (Weststadt)</span>
            </div>
            """;

        var flat = _parser.ParsePage(html).Should().ContainSingle().Subject.Flat;

        flat.PriceEur.Should().Be(389000);
        flat.RoomsCount.Should().Be(2.5);
        flat.LivingAreaSqm.Should().Be(74.5);
    }
}
