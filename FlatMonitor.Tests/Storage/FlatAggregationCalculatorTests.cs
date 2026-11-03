using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Models;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class FlatAggregationCalculatorTests
{
    [Test]
    public void Compute_GroupsByDistrictAndRoomsCount_ReturnsCorrectCounts()
    {
        // Arrange
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 550000, PricePerMeter = 5500, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 3, PriceEur = 700000, PricePerMeter = 5800, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(2);
        result.First(a => a.RoomsCount == 2)!.Count.Should().Be(2);
        result.First(a => a.RoomsCount == 3)!.Count.Should().Be(1);
    }

    [Test]
    public void Compute_ExcludesFlatsWithZeroOrNullPrice()
    {
        // Arrange
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 0, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = null, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(1);
        result.First().Count.Should().Be(1);
    }

    [Test]
    public void Compute_FlatsWithoutPricePerMeter_AreLeftOutOfAverageAndCount()
    {
        // Arrange: three flats, one without a living area (no price per m²)
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 420000, PricePerMeter = 6000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 600000, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert: Count is the number of flats in the average
        var aggregation = result.Should().ContainSingle().Subject;
        aggregation.AvgPricePerMeter.Should().Be(5500m);
        aggregation.Count.Should().Be(2);
    }

    [Test]
    public void Compute_GroupWhereNoFlatHasPricePerMeter_HasNoAggregation()
    {
        // Before: "2 flats, 0 €/m²", which the history chart drew as a real point
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 600000, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "2" },
            new() { District = "Oststadt", RoomsCount = 2, PriceEur = 400000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().ContainSingle().Which.District.Should().Be("Oststadt");
        result.Should().NotContain(a => a.AvgPricePerMeter == 0m);
    }

    [Test]
    public void Compute_ZeroPricePerMeter_IsLeftOut()
    {
        // A price of 0 gives 0 €/m²: not a real price
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 0, PricePerMeter = 0, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
        };

        FlatAggregationCalculator.Compute(snapshots).Should().BeEmpty();
    }

    [Test]
    public void Compute_OrdersByDistrictThenRoomsCount()
    {
        // Arrange
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Zentrum", RoomsCount = 3, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 3, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(3);
        result[0].District.Should().Be("Innenstadt");
        result[0].RoomsCount.Should().Be(2);
        result[1].District.Should().Be("Innenstadt");
        result[1].RoomsCount.Should().Be(3);
        result[2].District.Should().Be("Zentrum");
    }

    [Test]
    public void Compute_HandlesNullRoomsCount_AsDistinctGroup()
    {
        // Arrange
        var snapshots = new List<PublishedFlat>
        {
            new() { District = "Innenstadt", RoomsCount = null, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 550000, PricePerMeter = 5500, ScrapedAtUtc = DateTime.UtcNow, Source = "OhneMakler", ListingId = "2" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.RoomsCount == null);
        result.Should().Contain(a => a.RoomsCount == 2);
    }

    [Test]
    public void Compute_SameTitleOnTwoSources_IsCountedOnce()
    {
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Helle 3-Zimmer-Wohnung"),
            Flat("Immowelt", "iw-1", "Helle 3-Zimmer-Wohnung"),
        };

        var aggregation = FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Subject;

        aggregation.Count.Should().Be(1);
        aggregation.AvgPricePerMeter.Should().Be(5000m);
    }

    [Test]
    public void Compute_SameTitleOnThreeSources_IsCountedOnce()
    {
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Helle 3-Zimmer-Wohnung"),
            Flat("Immowelt", "iw-1", "Helle 3-Zimmer-Wohnung"),
            Flat("OhneMakler", "om-1", "Helle 3-Zimmer-Wohnung"),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(1);
    }

    [Test]
    public void Compute_SameTitleOnOneSource_AreDifferentFlats()
    {
        // an agent reuses the title for several units
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Mikroapartment in zentraler Lage", price: 119000, area: 30),
            Flat("ImmobilienScout24", "is-2", "Mikroapartment in zentraler Lage", price: 119000, area: 30),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Test]
    public void Compute_SameTitleOnSeveralSources_KeepsTheMostFlatsOfOneSource()
    {
        // two units on IS24, one of them also on Immowelt: two flats, not three
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Mikroapartment in zentraler Lage"),
            Flat("ImmobilienScout24", "is-2", "Mikroapartment in zentraler Lage"),
            Flat("Immowelt", "iw-1", "Mikroapartment in zentraler Lage"),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Test]
    public void Compute_TwoUnitsEachOnTwoSources_AreTwoFlats()
    {
        // seen in the database: "Mikroapartment in zentraler Lage", 2 on IS24 and 2 on Immowelt, all alike
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Mikroapartment in zentraler Lage", price: 119000, area: 30),
            Flat("ImmobilienScout24", "is-2", "Mikroapartment in zentraler Lage", price: 119000, area: 30),
            Flat("Immowelt", "iw-1", "Mikroapartment in zentraler Lage", price: 119000, area: 30),
            Flat("Immowelt", "iw-2", "Mikroapartment in zentraler Lage", price: 119000, area: 30),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Test]
    public void Compute_TitlesThatDifferOnlyInSpelling_AreTheSameFlat()
    {
        // seen in the database: entities, dashes and spaces differ between the portals
        var flats = new List<PublishedFlat>
        {
            Flat("OhneMakler", "om-1", "Wohnen wie im eigenen Haus – mitten in Karlsruhe &amp; Oststadt"),
            Flat("ImmobilienScout24", "is-1", "Wohnen wie im eigenen  Haus - mitten in Karlsruhe & Oststadt"),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(1);
    }

    [Test]
    public void Compute_DifferentTitles_AreDifferentFlats_EvenWithSamePriceAndArea()
    {
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Helle 3-Zimmer-Wohnung"),
            Flat("Immowelt", "iw-1", "Ruhige Wohnung mit Balkon"),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Test]
    public void Compute_SameTitleInDifferentPostalCodes_AreDifferentFlats()
    {
        // a generic title, e.g. Immowelt's "Über diese Immobilie"
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Über diese Immobilie", postalCode: "76133"),
            Flat("Immowelt", "iw-1", "Über diese Immobilie", postalCode: "76185"),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(2);
    }

    [Test]
    public void Compute_SameTitleInDifferentDistricts_IsCountedOnceInTheDistrictOfTheKeptSource()
    {
        // seen in the database: IS24 says Innenstadt-West, Immowelt Weststadt (same postal code).
        // On a tie the first source by name is kept: ImmobilienScout24
        var flats = new List<PublishedFlat>
        {
            Flat("Immowelt", "iw-1", "Bezugsfreie 3-Zimmer-Wohnung", district: "Weststadt"),
            Flat("ImmobilienScout24", "is-1", "Bezugsfreie 3-Zimmer-Wohnung", district: "Innenstadt-West"),
        };

        var aggregation = FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Subject;

        aggregation.District.Should().Be("Innenstadt-West");
        aggregation.Count.Should().Be(1);
    }

    [Test]
    public void Compute_SameTitleWithDifferentPrice_IsCountedOnceWithThePriceOfTheKeptSource()
    {
        // the price changed on one portal only
        var flats = new List<PublishedFlat>
        {
            Flat("Immowelt", "iw-1", "Top Lage mit Balkon", price: 265000, area: 50),
            Flat("ImmobilienScout24", "is-1", "Top Lage mit Balkon", price: 250000, area: 50),
        };

        var aggregation = FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Subject;

        aggregation.Count.Should().Be(1);
        aggregation.AvgPricePerMeter.Should().Be(5000m);
    }

    [Test]
    public void Compute_SameTitleWithDifferentRooms_IsCountedInTheRoomsGroupOfTheKeptSource()
    {
        var flats = new List<PublishedFlat>
        {
            Flat("Immowelt", "iw-1", "Helle Wohnung", rooms: 2.5),
            Flat("ImmobilienScout24", "is-1", "Helle Wohnung", rooms: 3),
        };

        var aggregation = FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Subject;

        aggregation.RoomsCount.Should().Be(3);
        aggregation.Count.Should().Be(1);
    }

    [Test]
    public void Compute_FlatsWithoutTitle_AreNeverDuplicates()
    {
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", null),
            Flat("Immowelt", "iw-1", null),
            Flat("OhneMakler", "om-1", "   "),
        };

        FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Which.Count.Should().Be(3);
    }

    [Test]
    public void Compute_DuplicatesAndOtherFlats_CountsEachFlatOnce()
    {
        var flats = new List<PublishedFlat>
        {
            Flat("ImmobilienScout24", "is-1", "Helle 3-Zimmer-Wohnung", price: 400000, area: 80),
            Flat("Immowelt", "iw-1", "Helle 3-Zimmer-Wohnung", price: 400000, area: 80),
            Flat("ImmobilienScout24", "is-2", "Kleines Apartment", price: 300000, area: 50),
            Flat("OhneMakler", "om-1", "Von privat", price: 350000, area: 70),
        };

        var aggregation = FlatAggregationCalculator.Compute(flats).Should().ContainSingle().Subject;

        aggregation.Count.Should().Be(3);
        aggregation.AvgPricePerMeter.Should().BeApproximately((5000m + 6000m + 5000m) / 3, 0.01m);
    }

    [TestCase("Helle 3-Zimmer-Wohnung", "helle 3 zimmer wohnung")]
    [TestCase("Wohnen – mitten in KA", "wohnen mitten in ka")]
    [TestCase("Gute &amp; gefragte Lage..", "gute gefragte lage")]
    [TestCase("Sonnige Wohnung      &quot;Durlach&quot;", "sonnige wohnung durlach")]
    [TestCase("Über diese Immobilie", "über diese immobilie")]
    [TestCase("  ", "")]
    [TestCase(null, "")]
    public void NormalizeTitle_RemovesWhatThePortalsWriteDifferently(string? title, string expected)
    {
        FlatAggregationCalculator.NormalizeTitle(title).Should().Be(expected);
    }

    private static PublishedFlat Flat(
        string source, string listingId, string? title,
        int price = 400000, double area = 80, string district = "Innenstadt", string postalCode = "76133",
        double rooms = 3) => new()
    {
        Source = source,
        ListingId = listingId,
        Title = title,
        District = district,
        PostalCode = postalCode,
        RoomsCount = rooms,
        PriceEur = price,
        LivingAreaSqm = area,
        PricePerMeter = (int)Math.Round(price / area),
        ScrapedAtUtc = DateTime.UtcNow,
    };
}

