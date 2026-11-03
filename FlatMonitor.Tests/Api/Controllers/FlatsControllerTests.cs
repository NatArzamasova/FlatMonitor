using FlatMonitor.Api.Controllers;
using FlatMonitor.Api.Contracts;
using FlatMonitor.Models;
using FlatMonitor.Storage;
using FlatMonitor.Storage.ReadModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Controllers;

[TestFixture]
public class FlatsControllerTests
{
    private Mock<IFlatRepository> _flatRepository = null!;
    private Mock<IFlatAggregationRepository> _aggregationRepository = null!;
    private Mock<IFlatPreviewRepository> _previewRepository = null!;
    private FlatsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _flatRepository = new Mock<IFlatRepository>();
        _aggregationRepository = new Mock<IFlatAggregationRepository>();
        _previewRepository = new Mock<IFlatPreviewRepository>();
        _controller = new FlatsController(_flatRepository.Object, _aggregationRepository.Object, _previewRepository.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [Test]
    public async Task GetHistoryAsync_ReturnsSuccessfulApiResponse()
    {
        var history = new List<FlatHistory> { new() { District = "Innenstadt", ScrapingDate = new DateOnly(2026, 10, 20) } };
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
        _flatRepository.Setup(r => r.GetCurrentOffersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(offers);

        var result = await _controller.GetCurrentOffersAsync();

        var response = GetResponseValue(result);
        response.Success.Should().BeTrue();
        response.Data.Should().BeSameAs(offers);
        response.Message.Should().Be("Current offers fetched successfully");
        _flatRepository.Verify(r => r.GetCurrentOffersAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task GetAggregatedAsync_LetsTheExceptionPropagateWhenRepositoryFails()
    {
        _aggregationRepository.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("query failed"));

        var act = () => _controller.GetAggregatedAsync();

        // ApiExceptionHandler turns it into a 500 with a generic message
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Actions_PassTheRequestCancellationTokenToTheRepositories()
    {
        using var cts = new CancellationTokenSource();
        _aggregationRepository.Setup(r => r.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _aggregationRepository.Setup(r => r.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _flatRepository.Setup(r => r.GetCurrentOffersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _controller.GetHistoryAsync(cts.Token);
        await _controller.GetAggregatedAsync(cts.Token);
        await _controller.GetCurrentOffersAsync(cts.Token);

        _aggregationRepository.Verify(r => r.GetHistoryAsync(cts.Token), Times.Once);
        _aggregationRepository.Verify(r => r.GetLatestAsync(cts.Token), Times.Once);
        _flatRepository.Verify(r => r.GetCurrentOffersAsync(cts.Token), Times.Once);
    }

    [Test]
    public async Task GetPreviewAsync_ReturnsTheStoredImageWithItsContentType()
    {
        var data = new byte[] { 0xFF, 0xD8, 0xFF };
        _previewRepository.Setup(r => r.GetAsync("ImmobilienScout24", "123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Preview("image/jpeg", data));

        var result = await _controller.GetPreviewAsync("ImmobilienScout24", "123");

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.FileContents.Should().Equal(data);
        file.ContentType.Should().Be("image/jpeg");
        _controller.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [Test]
    public async Task GetPreviewAsync_WithoutPreview_ReturnsNotFound()
    {
        _previewRepository.Setup(r => r.GetAsync("OhneMakler", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FlatPreview?)null);

        var result = await _controller.GetPreviewAsync("OhneMakler", "1");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task GetPreviewAsync_WithAStoredTypeThatIsNoPhoto_ReturnsNotFound()
    {
        // Script in an SVG would run on the API's origin
        _previewRepository.Setup(r => r.GetAsync("OhneMakler", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Preview("image/svg+xml", [1]));

        var result = await _controller.GetPreviewAsync("OhneMakler", "1");

        result.Should().BeOfType<NotFoundResult>();
    }

    private static FlatPreview Preview(string contentType, byte[] data) => new()
    {
        Source = "source", ListingId = "id", ImageUrl = "https://pictures.example/1.jpg", ContentType = contentType, Data = data,
    };

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
