namespace FlatMonitor.Tests.Immowelt;

using System.Text.Json;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Immowelt;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ImmoweltDetailScraperTests
{
    private const string Url = "https://www.immowelt.de/expose/0294187d-a3bf-49a2-990b-eb64ace539ed";
    private const string ListingId = "25DJIC4KVE5U";
    private const string DataDomePage = "<html><script src=\"https://geo.captcha-delivery.com/captcha/\"></script></html>";

    private static readonly string ExposePage = ImmoweltFixtures.Read("expose-page.html");

    private string _dataDir = null!;
    private Mock<IPage> _page = null!;
    private List<string> _openedUrls = null!;
    private int? _status;
    private string _html = null!;
    private ListLogger<ImmoweltDetailScraperTests> _logger = null!;
    private HtmlPageArchive _htmlPages = null!;
    private ImmoweltDetailScraper _scraper = null!;

    [SetUp]
    public void Setup()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"ImmoweltDetailScraperTests_{Guid.NewGuid()}");
        _logger = new ListLogger<ImmoweltDetailScraperTests>();
        _htmlPages = HtmlPageArchive.ForSource(new StorageOptions(_dataDir, "Data Source=:memory:"), ImmoweltPages.SourceName, _logger);
        _openedUrls = [];
        _status = 200;
        _html = ExposePage;

        _page = new Mock<IPage>(MockBehavior.Strict);
        _page.Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .Callback((string url, PageGotoOptions? _) => _openedUrls.Add(url))
            .ReturnsAsync(() => _status is { } status ? Mock.Of<IResponse>(r => r.Status == status) : null);
        _page.Setup(p => p.ContentAsync()).ReturnsAsync(() => _html);

        _scraper = new ImmoweltDetailScraper(_page.Object, new ImmoweltOptions { PauseBetweenPagesMs = 0 }, _htmlPages, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    private static Flat Card(string? url = Url, string listingId = ListingId) => new()
    {
        Source = "Immowelt",
        ListingId = listingId,
        Url = url,
        PriceEur = 310000,
        OriginalPriceEur = 310000,
        RoomsCount = 3,
        LivingAreaSqm = 75.04,
        ScrapedAtUtc = DateTime.UtcNow,
    };

    /// <summary>An expose page whose data script holds <paramref name="classifiedJson"/>.</summary>
    private static string ExposeHtml(string classifiedJson)
    {
        var json = """{"app_demand_referral_cldp":{"data":{"classified":""" + classifiedJson + "}}}";
        return $"<script>window[\"__UFRN_LIFECYCLE_SERVERREQUEST__\"]=JSON.parse({JsonSerializer.Serialize(json)});</script>";
    }

    [Test]
    public async Task EnrichAsync_AppliesTheExposeToTheCard()
    {
        var card = Card();

        var flat = await _scraper.EnrichAsync(card, default);

        flat.Should().BeEquivalentTo(new ImmoweltDetailParser().Parse(ExposePage)!.ApplyTo(card));
        flat!.PriceEur.Should().Be(305000, "the expose's price replaces the card's");
        flat.Details.Should().NotBeNull();
        _openedUrls.Should().Equal(Url);
    }

    [Test]
    public async Task EnrichAsync_WaitsOnlyForTheHtml()
    {
        await _scraper.EnrichAsync(Card(), default);

        _page.Verify(p => p.GotoAsync(Url, It.Is<PageGotoOptions>(o => o.WaitUntil == WaitUntilState.DOMContentLoaded)), Times.Once);
    }

    [Test]
    public async Task EnrichAsync_SavesTheExposeUnderTheListingId()
    {
        await _scraper.EnrichAsync(Card(), default);

        var file = Directory.GetFiles(_htmlPages.Directory).Should().ContainSingle().Subject;
        Path.GetFileName(file).Should().Contain(ListingId);
        (await File.ReadAllTextAsync(file)).Should().Be(ExposePage);
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task EnrichAsync_CardWithoutUrl_KeepsTheCardWithoutOpeningAPage(string? url)
    {
        var card = Card(url);

        var flat = await _scraper.EnrichAsync(card, default);

        flat.Should().BeSameAs(card);
        _openedUrls.Should().BeEmpty();
    }

    [TestCase(404)]
    [TestCase(410)]
    public async Task EnrichAsync_ExposeGone_LeavesTheFlatOut(int status)
    {
        _status = status;

        (await _scraper.EnrichAsync(Card(), default)).Should().BeNull();
    }

    [Test]
    public async Task EnrichAsync_UnpublishedListing_LeavesTheFlatOut()
    {
        _html = ExposeHtml("""{"metadata":{"status":{"status":false}},"sections":{}}""");

        (await _scraper.EnrichAsync(Card(), default)).Should().BeNull();
    }

    [Test]
    public async Task EnrichAsync_ExposeWithoutData_ThrowsSoTheCardIsKept()
    {
        _html = "<html><body>Neue Seite</body></html>";

        var act = () => _scraper.EnrichAsync(Card(), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{ListingId}*has the page changed*");
    }

    [Test]
    public async Task EnrichAsync_BlockedByDataDome_KeepsTheCardsAndOpensNoFurtherExpose()
    {
        _html = DataDomePage;
        var first = Card(listingId: "A1");
        var second = Card(url: Url + "-2", listingId: "B2");

        var firstFlat = await _scraper.EnrichAsync(first, default);
        var secondFlat = await _scraper.EnrichAsync(second, default);

        firstFlat.Should().BeSameAs(first);
        secondFlat.Should().BeSameAs(second);
        _openedUrls.Should().ContainSingle();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning)
            .Which.Message.Should().Contain("DataDome").And.Contain("robot check");
    }

    [Test]
    public async Task EnrichAsync_WhenCanceled_OpensNoPage()
    {
        var act = () => _scraper.EnrichAsync(Card(), new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
        _openedUrls.Should().BeEmpty();
    }
}
