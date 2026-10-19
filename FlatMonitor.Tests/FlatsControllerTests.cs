using NUnit.Framework;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using workInProgress.Api.Controllers;
using workInProgress.Api.Models;
using workInProgress.Data;
using workInProgress.Models;

namespace workInProgress.Tests;

[TestFixture]
public class FlatsControllerTests
{
    private FlatsDbContext _dbContext = null!;
    private FlatsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new FlatsDbContext(options);
        _dbContext.Database.EnsureCreated();
        _controller = new FlatsController(_dbContext);
    }

    [TearDown]
    public void Teardown()
    {
        _dbContext?.Dispose();
    }

    private ApiResponse<List<FlatAggregation>>? GetResponseValue(ActionResult<ApiResponse<List<FlatAggregation>>> result)
    {
        if (result.Value is not null)
        {
            return result.Value;
        }

        // If Value is null, check if it's an OkObjectResult
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        return okResult!.Value as ApiResponse<List<FlatAggregation>>;
    }

    [Test]
    public async Task GetAggregatedAsync_ReturnsDataFromFlatAggregationsTable()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Innenstadt",
            RoomsCount = 2,
            Count = 5,
            AvgPricePerMeter = 5000m,
            ScrapingDate = today
        });
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Zentrum",
            RoomsCount = 3,
            Count = 3,
            AvgPricePerMeter = 6000m,
            ScrapingDate = today
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAggregatedAsync();
        var response = GetResponseValue(result);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);

        var innenstadt = response.Data!.First(a => a.District == "Innenstadt");
        innenstadt.Count.Should().Be(5);
        innenstadt.AvgPricePerMeter.Should().Be(5000m);
        innenstadt.PostalCode.Should().BeNull();
        innenstadt.Availability.Should().BeNull();
    }

    [Test]
    public async Task GetAggregatedAsync_ReturnsEmptyList_WhenTableIsEmpty()
    {
        // Act
        var result = await _controller.GetAggregatedAsync();
        var response = GetResponseValue(result);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().BeEmpty();
    }

    [Test]
    public async Task GetAggregatedAsync_OrdersByDistrictThenRoomsCount()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Zentrum",
            RoomsCount = 3,
            Count = 3,
            AvgPricePerMeter = 6000m,
            ScrapingDate = today
        });
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Innenstadt",
            RoomsCount = 3,
            Count = 2,
            AvgPricePerMeter = 5500m,
            ScrapingDate = today
        });
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Innenstadt",
            RoomsCount = 2,
            Count = 5,
            AvgPricePerMeter = 5000m,
            ScrapingDate = today
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAggregatedAsync();
        var response = GetResponseValue(result);

        // Assert
        response!.Data.Should().HaveCount(3);
        response.Data![0].District.Should().Be("Innenstadt");
        response.Data![0].RoomsCount.Should().Be(2);
        response.Data![1].District.Should().Be("Innenstadt");
        response.Data![1].RoomsCount.Should().Be(3);
        response.Data![2].District.Should().Be("Zentrum");
        response.Data![2].RoomsCount.Should().Be(3);
    }

    [Test]
    public async Task GetAggregatedAsync_MapsPostalCodeAndAvailabilityToNull()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Innenstadt",
            RoomsCount = 2,
            Count = 5,
            AvgPricePerMeter = 5000m,
            ScrapingDate = today
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAggregatedAsync();
        var response = GetResponseValue(result);

        // Assert
        var aggregation = response!.Data!.First();
        aggregation.PostalCode.Should().BeNull();
        aggregation.Availability.Should().BeNull();
    }

    [Test]
    public async Task GetAggregatedAsync_HandlesNullDistrict()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = null,
            RoomsCount = 2,
            Count = 5,
            AvgPricePerMeter = 5000m,
            ScrapingDate = today
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAggregatedAsync();
        var response = GetResponseValue(result);

        // Assert
        response!.Data.Should().HaveCount(1);
        response.Data![0].District.Should().BeNull();
    }

    [Test]
    public async Task GetAggregatedAsync_HandlesNullRoomsCount()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.FlatAggregations.Add(new FlatAggregationSnapshot
        {
            District = "Innenstadt",
            RoomsCount = null,
            Count = 5,
            AvgPricePerMeter = 5000m,
            ScrapingDate = today
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetAggregatedAsync();
        var response = GetResponseValue(result);

        // Assert
        response!.Data.Should().HaveCount(1);
        response.Data![0].RoomsCount.Should().BeNull();
    }
}
