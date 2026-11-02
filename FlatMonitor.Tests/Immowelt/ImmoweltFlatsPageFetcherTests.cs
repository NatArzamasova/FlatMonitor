namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ImmoweltFlatsPageFetcherTests
{
    private const string SearchUrl = "https://www.immowelt.de/classified-search?distributionTypes=Buy";
    private const string DataDomePage = "<html><script src=\"https://geo.captcha-delivery.com/captcha/\"></script></html>";
    private const string ChangedPage = "<html><body>Neue Seite</body></html>";

    /// <summary>Results page 2 of the saved search: 268 flats in total → 9 pages, 29 flats on this page.</summary>
    private static readonly string ListPage = ImmoweltFixtures.Read("list-page.html");

    private Mock<IPage> _page = null!;
    private List<string> _openedUrls = null!;

    /// <summary>The HTML per opened URL; a URL not in here gets the list page.</summary>
    private Dictionary<string, string> _htmlByUrl = null!;

    [SetUp]
    public void Setup()
    {
        _page = new Mock<IPage>(MockBehavior.Strict);
        _openedUrls = [];
        _htmlByUrl = [];

        _page.Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .Callback((string url, PageGotoOptions? _) => _openedUrls.Add(url))
            .ReturnsAsync((IResponse?)null);
        _page.Setup(p => p.ContentAsync())
            .ReturnsAsync(() => _htmlByUrl.GetValueOrDefault(_openedUrls[^1], ListPage));
    }

    private ImmoweltFlatsPageFetcher CreateFetcher() =>
        new(_page.Object, new ImmoweltOptions { SearchUrl = SearchUrl, PauseBetweenPagesMs = 0 }, NullLogger.Instance);

    private static string PageUrl(int page) => ImmoweltPages.PageUrl(SearchUrl, page);

    [Test]
    public async Task FetchAllPagesAsync_OpensEveryPageOfTheTotalCountByUrl()
    {
        var pages = await CreateFetcher().FetchAllPagesAsync().ToListAsync();

        pages.Should().HaveCount(9).And.OnlyContain(page => page.Flats.Count == 29);
        _openedUrls.Should().Equal(Enumerable.Range(1, 9).Select(PageUrl));
        _openedUrls[0].Should().Be(SearchUrl, "page 1 is the search URL itself");
    }

    [Test]
    public async Task FetchAllPagesAsync_WaitsOnlyForTheHtml()
    {
        await CreateFetcher().FetchAllPagesAsync().FirstAsync();

        _page.Verify(p => p.GotoAsync(SearchUrl,
            It.Is<PageGotoOptions>(o => o.WaitUntil == WaitUntilState.DOMContentLoaded)), Times.Once);
    }

    [Test]
    public async Task FetchAllPagesAsync_WithDataDomeOnTheFirstPage_ThrowsAndSaysWhatToDo()
    {
        _htmlByUrl[PageUrl(1)] = DataDomePage;

        var act = () => CreateFetcher().FetchAllPagesAsync().ToListAsync().AsTask();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*page 1*DataDome*robot check*SaveDebugPages*");
        _openedUrls.Should().ContainSingle();
    }

    [Test]
    public async Task FetchAllPagesAsync_WithoutDataOnALaterPage_ThrowsAfterTheEarlierPages()
    {
        _htmlByUrl[PageUrl(3)] = ChangedPage;
        var pages = new List<ImmoweltListPage>();

        var act = async () =>
        {
            await foreach (var page in CreateFetcher().FetchAllPagesAsync())
                pages.Add(page);
        };

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*page 3*has the page changed*");
        pages.Should().HaveCount(2);
        _openedUrls.Should().HaveCount(3, "no page after the failed one is opened");
    }

    [Test]
    public async Task FetchAllPagesAsync_WhenCanceled_OpensNoPage()
    {
        var act = () => CreateFetcher().FetchAllPagesAsync(new CancellationToken(canceled: true)).ToListAsync().AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        _openedUrls.Should().BeEmpty();
    }

    [Test]
    public async Task FetchAllPagesAsync_WhenCanceledDuringThePause_OpensNoFurtherPage()
    {
        using var cts = new CancellationTokenSource();
        var fetcher = new ImmoweltFlatsPageFetcher(
            _page.Object, new ImmoweltOptions { SearchUrl = SearchUrl, PauseBetweenPagesMs = 60_000 }, NullLogger.Instance);

        var act = async () =>
        {
            await foreach (var _ in fetcher.FetchAllPagesAsync(cts.Token))
                cts.CancelAfter(50);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        _openedUrls.Should().ContainSingle();
    }

    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(30, 1)]
    [TestCase(31, 2)]
    [TestCase(268, 9)]
    [TestCase(1_500, 50)]
    [TestCase(100_000, 50)]
    public void LastPage_IsTheTotalCountInPagesOf30_CappedAt50(int totalCount, int expected)
    {
        ImmoweltFlatsPageFetcher.LastPage(totalCount).Should().Be(expected);
    }
}
