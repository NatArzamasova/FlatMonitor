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
}
