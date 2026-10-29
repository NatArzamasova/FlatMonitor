namespace FlatMonitor.Tests.OhneMakler;

using FlatMonitor.Models;
using FlatMonitor.Scraping.OhneMakler;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class OhneMaklerKarlsruheResultsTests
{
    private static Flat Card(string id, string? postalCode) => new()
    {
        Source = OhneMaklerScraper.SourceName,
        ListingId = id,
        PostalCode = postalCode,
        ScrapedAtUtc = DateTime.UtcNow,
    };

    [Test]
    public void TakeUntilOutsideKarlsruhe_AllInKarlsruhe_KeepsAllCards()
    {
        var cards = new[] { Card("1", "76133"), Card("2", "76227") };

        var kept = OhneMaklerKarlsruheResults.TakeUntilOutsideKarlsruhe(cards, out var firstOutside);

        kept.Should().Equal(cards);
        firstOutside.Should().BeNull();
    }

    [Test]
    public void TakeUntilOutsideKarlsruhe_StopsAtTheFirstCardOutside_AndDropsEverythingAfterIt()
    {
        var cards = new[] { Card("1", "76133"), Card("2", "76344"), Card("3", "76131") };

        var kept = OhneMaklerKarlsruheResults.TakeUntilOutsideKarlsruhe(cards, out var firstOutside);

        kept.Select(c => c.ListingId).Should().Equal("1");
        firstOutside!.ListingId.Should().Be("2");
    }

    [TestCase(null)]
    [TestCase("")]
    public void TakeUntilOutsideKarlsruhe_KeepsCardsWithoutPostcode(string? postalCode)
    {
        var cards = new[] { Card("1", postalCode), Card("2", "76133") };

        var kept = OhneMaklerKarlsruheResults.TakeUntilOutsideKarlsruhe(cards, out var firstOutside);

        kept.Should().HaveCount(2);
        firstOutside.Should().BeNull();
    }

    [Test]
    public void TakeUntilOutsideKarlsruhe_FirstCardOutside_KeepsNothing()
    {
        var kept = OhneMaklerKarlsruheResults.TakeUntilOutsideKarlsruhe([Card("1", "75175")], out var firstOutside);

        kept.Should().BeEmpty();
        firstOutside.Should().NotBeNull();
    }
}
