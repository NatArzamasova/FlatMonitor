
using NUnit.Framework;
using FluentAssertions;
using workInProgress.Scraping;

namespace workInProgress.Tests;
[TestFixture]
public class ListingCardParserTests
{
    private ListingCardParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ListingCardParser();
    }

    private string GetFixturePath(string filename)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", filename);
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
        var firstListing = result[0].Listing;
        firstListing.ListingId.Should().Be("502814");
        firstListing.Url.Should().Be("/immobilie/502814/");
        firstListing.Title.Should().Contain("Altbauperle");
        firstListing.PostalCode.Should().Be("76135");
        firstListing.District.Should().Contain("Weststadt");
        firstListing.PriceEur.Should().Be(270000);
        firstListing.RoomsCount.Should().Be(3);
        firstListing.LivingAreaSqm.Should().Be(82);
    }

    [Test]
    public void ParsePage_WithMissingRoomCount_ReturnsNullForRooms()
    {
        // Arrange
        var html = File.ReadAllText(GetFixturePath("listings-page-sample.html"));

        // Act
        var result = _parser.ParsePage(html);

        // Assert
        var thirdListing = result[2].Listing;
        thirdListing.ListingId.Should().Be("502816");
        thirdListing.RoomsCount.Should().BeNull();
        thirdListing.LivingAreaSqm.Should().Be(78);
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
            item.Listing.ScrapedAtUtc.Should().NotBeNullOrEmpty();
            // Should be ISO-8601 format
            DateTime.TryParse(item.Listing.ScrapedAtUtc, out _).Should().BeTrue();
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
