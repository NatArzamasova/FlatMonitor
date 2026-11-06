using FlatMonitor.Models;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests.Models;

[TestFixture]
public class FlatRecordTests
{
    private static PublishedFlat Published() => PublishedFlat.From(new Flat
    {
        Source = "ImmobilienScout24",
        ListingId = "42",
        Url = "https://example.org/42",
        Title = "Sunny flat",
        Address = "Kaiserstraße 1",
        PostalCode = "76133",
        District = "Innenstadt-West",
        PriceEur = 300_000,
        OriginalPriceEur = 320_000,
        RoomsCount = 3,
        LivingAreaSqm = 75,
        CommissionPercent = 3.57,
        Details = new FlatDetails { Heating = "Gas", BuildYear = 1990 },
        ScrapedAtUtc = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc),
    });

    [Test]
    public void ArchivedFlatFrom_CopiesEveryFieldAndSetsTheRun()
    {
        var published = Published();

        var archived = ArchivedFlat.From(published, scrapingHistoryId: 75);

        archived.ScrapingHistoryId.Should().Be(75);
        archived.Should().BeEquivalentTo(published);
    }

    [Test]
    public void ArchivedFlatFrom_GivesTheRowItsOwnDetails()
    {
        // EF tracks owned details per owner: one instance in two rows is tracked as both
        var published = Published();

        var archived = ArchivedFlat.From(published, scrapingHistoryId: 75);

        archived.Details.Should().NotBeSameAs(published.Details);
        archived.Details.Should().BeEquivalentTo(published.Details);
    }

    [Test]
    public void ArchivedFlatFrom_WithoutDetails_HasNone()
    {
        var published = PublishedFlat.From(new Flat { Source = "OhneMakler", ListingId = "1", ScrapedAtUtc = DateTime.UtcNow });

        ArchivedFlat.From(published, scrapingHistoryId: 75).Details.Should().BeNull();
    }
}
