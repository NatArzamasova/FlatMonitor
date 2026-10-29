namespace FlatMonitor.Tests.OhneMakler;

using System.Net;
using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Scraping.OhneMakler;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Where <see cref="OhneMaklerScraper"/> ends pagination: the fetcher only passes pages on, the scraper parses each
/// page once and stops at the first one without cards. Detail pages answer 404, so the cards are kept as they are.
/// </summary>
[TestFixture]
public class OhneMaklerScraperPaginationTests
{
    private const string EmptyPage = "<html><body></body></html>";

    private string _dataDir = null!;
    private string _listingsHtml = null!;

    [SetUp]
    public async Task Setup()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"OhneMaklerScraperPaginationTests_{Guid.NewGuid()}");
        _listingsHtml = await File.ReadAllTextAsync(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "OhneMakler", "listings-page-sample.html"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    [Test]
    public async Task ScrapeAsync_StopsAtThePageWithoutCardsAndFetchesNoFurther()
    {
        // Arrange: the fixture's cards on pages 1 and 2 (with other ids on page 2), nothing on page 3
        var page2 = _listingsHtml.Replace("/immobilie/", "/immobilie/9");
        var handler = new PageHandler(page => page switch { 1 => _listingsHtml, 2 => page2, _ => EmptyPage });

        // Act
        var flats = await CreateScraper(handler).ScrapeAsync().ToListAsync();

        // Assert
        handler.RequestedPages.Should().Equal(1, 2, 3);
        flats.Should().HaveCount(handler.CardsOnPage1 * 2);
    }

    [Test]
    public async Task ScrapeAsync_WhenTheFirstPageHasNoCards_Throws()
    {
        // Arrange
        var handler = new PageHandler(_ => EmptyPage);

        // Act
        var act = () => CreateScraper(handler).ScrapeAsync().ToListAsync().AsTask();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No listings on the first page*");
        handler.RequestedPages.Should().Equal(1);
    }

    private OhneMaklerScraper CreateScraper(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new StorageOptions(_dataDir, "Data Source=:memory:"), NullLogger<OhneMaklerScraper>.Instance);

    /// <summary>Answers the results pages with <c>page(n)</c> and every detail page with 404.</summary>
    private sealed class PageHandler(Func<int, string> page) : HttpMessageHandler
    {
        public List<int> RequestedPages { get; } = [];

        public int CardsOnPage1 => new OhneMaklerFlatCardParser().ParsePage(page(1)).Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath.Contains("/immobilie/"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            int number = uri.Query.StartsWith("?page=") ? int.Parse(uri.Query["?page=".Length..]) : 1;
            RequestedPages.Add(number);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page(number)) });
        }
    }
}
