using NUnit.Framework;
using FluentAssertions;
using workInProgress.Models;
using workInProgress.Storage;

namespace workInProgress.Tests;

[TestFixture]
public class FlatAggregationCalculatorTests
{
    [Test]
    public void Compute_GroupsByDistrictAndRoomsCount_ReturnsCorrectCounts()
    {
        // Arrange
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 550000, PricePerMeter = 5500, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 3, PriceEur = 700000, PricePerMeter = 5800, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(2);
        result.First(a => a.RoomsCount == 2)!.Count.Should().Be(2);
        result.First(a => a.RoomsCount == 3)!.Count.Should().Be(1);
    }

    [Test]
    public void Compute_ExcludesSnapshotsWithNullDistrict()
    {
        // Arrange
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = null, RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(1);
        result.First().District.Should().Be("Innenstadt");
    }

    [Test]
    public void Compute_ExcludesSnapshotsWithZeroOrNullPrice()
    {
        // Arrange
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 0, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = null, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "3" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(1);
        result.First().Count.Should().Be(1);
    }

    [Test]
    public void Compute_AvgPricePerMeter_IgnoresSnapshotsWithoutPricePerMeter()
    {
        // Arrange
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 600000, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(1);
        result.First().AvgPricePerMeter.Should().Be(5000m);
    }

    [Test]
    public void Compute_AvgPricePerMeter_IsZero_WhenNoSnapshotHasPricePerMeter()
    {
        // Arrange
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 600000, PricePerMeter = null, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(1);
        result.First().AvgPricePerMeter.Should().Be(0m);
    }

    [Test]
    public void Compute_OrdersByDistrictThenRoomsCount()
    {
        // Arrange
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Zentrum", RoomsCount = 3, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 3, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "3" },
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
        var snapshots = new List<FlatSnapshot>
        {
            new() { District = "Innenstadt", RoomsCount = null, PriceEur = 500000, PricePerMeter = 5000, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "1" },
            new() { District = "Innenstadt", RoomsCount = 2, PriceEur = 550000, PricePerMeter = 5500, ScrapedAtUtc = DateTime.UtcNow.ToString("O"), Source = "OhneMakler", ListingId = "2" },
        };

        // Act
        var result = FlatAggregationCalculator.Compute(snapshots);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.RoomsCount == null);
        result.Should().Contain(a => a.RoomsCount == 2);
    }
}
