using FlatMonitor.Api.Controllers;
using FlatMonitor.Api.Models;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Controllers;

[TestFixture]
public class FlatsControllerTests
{
    private Mock<ISnapshotRepository> _snapshotRepository = null!;
    private Mock<IFlatAggregationRepository> _aggregationRepository = null!;
    private FlatsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _snapshotRepository = new Mock<ISnapshotRepository>();
        _aggregationRepository = new Mock<IFlatAggregationRepository>();
        _controller = new FlatsController(_snapshotRepository.Object, _aggregationRepository.Object);
    }

    [Test]
    public async Task GetHistoryAsync_ReturnsSuccessfulApiResponse()
    {
        var history = new List<FlatHistoryDto> { new() { District = "Innenstadt", ScrapingDate = "2026-10-20" } };
        _aggregationRepository.Setup(r => r.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(history);

        var result = await _controller.GetHistoryAsync();

        var response = GetResponseValue(result);
        response.Success.Should().BeTrue();
        response.Data.Should().BeSameAs(history);
        response.Message.Should().Be("History fetched successfully");
        _aggregationRepository.Verify(r => r.GetHistoryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetCurrentOffersAsync_ReturnsSuccessfulApiResponse()
    {
        var offers = new List<FlatOffer> { new() { District = "Zentrum", Source = "source" } };
        _snapshotRepository.Setup(r => r.GetCurrentOffersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(offers);

        var result = await _controller.GetCurrentOffersAsync();

        var response = GetResponseValue(result);
        response.Success.Should().BeTrue();
        response.Data.Should().BeSameAs(offers);
        response.Message.Should().Be("Current offers fetched successfully");
        _snapshotRepository.Verify(r => r.GetCurrentOffersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAggregatedAsync_ReturnsSuccessfulApiResponse()
    {
        var aggregations = new List<FlatAggregation> { new() { District = "Innenstadt", Count = 2 } };
        _aggregationRepository.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(aggregations);

        var result = await _controller.GetAggregatedAsync();

        var response = GetResponseValue(result);
        response.Success.Should().BeTrue();
        response.Data.Should().BeSameAs(aggregations);
        response.Message.Should().Be("Aggregated successfully");
        _aggregationRepository.Verify(r => r.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAggregatedAsync_ReturnsInternalServerErrorWhenRepositoryFails()
    {
        _aggregationRepository.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("query failed"));

        var result = await _controller.GetAggregatedAsync();

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
        var response = ((ObjectResult)result.Result!).Value.Should().BeOfType<ApiResponse<List<FlatAggregation>>>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("query failed");
    }

    private static TResponse GetResponseValue<TResponse>(ActionResult<TResponse> result)
        where TResponse : class
    {
        if (result.Value is not null)
            return result.Value;

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        return okResult!.Value.Should().BeOfType<TResponse>().Subject;
    }
}
