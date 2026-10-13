namespace workInProgress.Tests.OhneMakler;

using NUnit.Framework;
using FluentAssertions;
using workInProgress.Scraping.OhneMakler;

[TestFixture]
public class OhneMaklerListingCardParserTests
{
    private OhneMaklerListingCardParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new OhneMaklerListingCardParser();
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "OhneMakler", filename);
    }

    [Test]
    public void ParsePage_WithValidListings_ReturnsCorrectCount()
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
    public void ParsePage_WithValidListings_ExtractsListingDetailsCorrectly()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        var firstFlat = result[0].Flat;
        firstFlat.ListingId.Should().Be("502814");
        firstFlat.Url.Should().Be("/immobilie/502814/");
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
        var html = File.ReadAllText("Fixtures/listings-page-sample.html");

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
        var html = File.ReadAllText("Fixtures/listings-page-sample.html");

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Should().AllSatisfy(item =>
        {
            item.Flat.ScrapedAtUtc.Should().NotBeNullOrEmpty();
            // Should be ISO-8601 format
            DateTime.TryParse(item.Flat.ScrapedAtUtc, out _).Should().BeTrue();
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
    public void ParsePage_WithMalformedListing_SkipsIt()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href=""/immobilie/123/"">Valid Listing</a>
                    <a href=""#"">Invalid Link</a>
                    <a href=""/immobilie/456/"">Another Valid</a>
                </body>
            </html>";

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        result.Count.Should().BeGreaterThanOrEqualTo(1);
    }
}
