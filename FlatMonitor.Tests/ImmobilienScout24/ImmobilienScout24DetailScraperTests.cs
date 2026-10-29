namespace FlatMonitor.Tests.ImmobilienScout24;

using FlatMonitor.Scraping.ImmobilienScout24;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ImmobilienScout24DetailScraperTests
{
    private const string Url = "https://www.immobilienscout24.de/expose/123";
    private const string Html = "<html><body><dd class='is24qa-typ'>Etagenwohnung</dd></body></html>";

    private string _dataDir = null!;
    private Mock<IPage> _page = null!;
    private ListLogger<ImmobilienScout24DetailScraperTests> _logger = null!;
    private HtmlSnapshotStore _snapshots = null!;
    private ImmobilienScout24DetailScraper _scraper = null!;

    [SetUp]
    public void Setup()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"ImmobilienScout24DetailScraperTests_{Guid.NewGuid()}");
        _logger = new ListLogger<ImmobilienScout24DetailScraperTests>();
        _snapshots = HtmlSnapshotStore.ForSource(new StorageOptions(_dataDir, "Data Source=:memory:"), ImmobilienScout24Scraper.SourceName, _logger);

        _page = new Mock<IPage>(MockBehavior.Strict);
        _page.Setup(p => p.GotoAsync(Url, It.IsAny<PageGotoOptions>())).ReturnsAsync((IResponse?)null);
        _page.Setup(p => p.WaitForSelectorAsync(It.IsAny<string>(), It.IsAny<PageWaitForSelectorOptions>())).ReturnsAsync((IElementHandle?)null);
        _page.Setup(p => p.ContentAsync()).ReturnsAsync(Html);

        _scraper = new ImmobilienScout24DetailScraper(_page.Object, _snapshots, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    [Test]
    public async Task FetchDetailHtmlAsync_ReturnsThePageHtmlAndSavesItAsSnapshot()
    {
        var html = await _scraper.FetchDetailHtmlAsync(Url, "123");

        html.Should().Be(Html);
        var snapshot = Directory.GetFiles(_snapshots.Directory, "123_*.html").Should().ContainSingle().Subject;
        File.ReadAllText(snapshot).Should().Be(Html);
        _page.Verify(p => p.ContentAsync(), Times.Once);
    }

    [Test]
    public async Task FetchDetailHtmlAsync_WaitsForTheExposeAttributesInsteadOfAFixedDelay()
    {
        await _scraper.FetchDetailHtmlAsync(Url, "123");

        _page.Verify(p => p.WaitForSelectorAsync(
            ImmobilienScout24DetailScraper.ExposeContentSelector,
            It.Is<PageWaitForSelectorOptions>(o => o.State == WaitForSelectorState.Attached && o.Timeout == ImmobilienScout24DetailScraper.ExposeContentTimeoutMs)),
            Times.Once);
    }

    [Test]
    public async Task FetchDetailHtmlAsync_WhenTheExposeAttributesNeverAppear_StillReturnsThePage()
    {
        _page.Setup(p => p.WaitForSelectorAsync(It.IsAny<string>(), It.IsAny<PageWaitForSelectorOptions>()))
            .ThrowsAsync(new TimeoutException("Timeout 5000ms exceeded."));

        var html = await _scraper.FetchDetailHtmlAsync(Url, "123");

        html.Should().Be(Html);
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Test]
    public async Task FetchDetailHtmlAsync_WhenThePageCannotBeOpened_Throws()
    {
        // FlatScraper logs it once and keeps the card's data
        _page.Setup(p => p.GotoAsync(Url, It.IsAny<PageGotoOptions>()))
            .ThrowsAsync(new PlaywrightException("net::ERR_CONNECTION_RESET"));

        var act = () => _scraper.FetchDetailHtmlAsync(Url, "123");

        await act.Should().ThrowAsync<PlaywrightException>();
        Directory.Exists(_snapshots.Directory).Should().BeFalse();
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }
}
