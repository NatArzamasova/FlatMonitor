namespace FlatMonitor.Tests.ImmobilienScout24;

using FlatMonitor.Scraping.ImmobilienScout24;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ImmobilienScout24FlatsPageFetcherTests
{
    private const string NextButtonSelector = "button[data-testid='pagination-button-next']:not([disabled])";
    private const string PageWithListings = "<html><body><div class='listing-card' data-obid='2'></div></body></html>";
    private const string PageWithoutListings = "<html><body></body></html>";

    private Mock<IPage> _page = null!;
    private Queue<IElementHandle?> _nextButtons = null!;

    [SetUp]
    public void Setup()
    {
        // Loose: the remaining calls (load state, waiting, the previous first card) complete with defaults
        _page = new Mock<IPage>();
        _nextButtons = new Queue<IElementHandle?>();
        _page.Setup(p => p.QuerySelectorAsync(NextButtonSelector, It.IsAny<PageQuerySelectorOptions>()))
            .ReturnsAsync(() => _nextButtons.Count > 0 ? _nextButtons.Dequeue() : null);
        _page.Setup(p => p.ContentAsync()).ReturnsAsync(PageWithListings);
    }

    private ImmobilienScout24FlatsPageFetcher CreateFetcher() => new(_page.Object, NullLogger.Instance);

    private void AddNextButtons(int count)
    {
        for (int i = 0; i < count; i++)
            _nextButtons.Enqueue(Mock.Of<IElementHandle>());
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WithoutANextButton_YieldsNothing()
    {
        // Act
        var pages = await CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync();

        // Assert
        pages.Should().BeEmpty();
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_YieldsEachPageUntilThereIsNoNextButton()
    {
        // Arrange
        AddNextButtons(2);

        // Act
        var pages = await CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync();

        // Assert
        pages.Should().Equal(PageWithListings, PageWithListings);
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_PassesEachPageOnWithoutParsingIt()
    {
        // Arrange: the caller (the scraper session) parses each page once and decides where pagination ends
        AddNextButtons(2);
        _page.SetupSequence(p => p.ContentAsync()).ReturnsAsync(PageWithListings).ReturnsAsync(PageWithoutListings);

        // Act
        var pages = await CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync();

        // Assert
        pages.Should().Equal(PageWithListings, PageWithoutListings);
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenTheNextPageDoesNotLoad_ThrowsAfterYieldingTheEarlierPages()
    {
        // Arrange: page 2 loads, page 3 times out
        AddNextButtons(2);
        _page.SetupSequence(p => p.WaitForFunctionAsync(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<PageWaitForFunctionOptions>()))
            .ReturnsAsync(Mock.Of<IJSHandle>())
            .ThrowsAsync(new TimeoutException("Timeout 60000ms exceeded"));
        var pages = new List<string>();

        // Act
        var act = async () =>
        {
            await foreach (var page in CreateFetcher().FetchAllPagesHtmlAsync())
                pages.Add(page);
        };

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        pages.Should().Equal(PageWithListings);
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenCanceled_ThrowsOperationCanceled()
    {
        // Arrange
        AddNextButtons(1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => CreateFetcher().FetchAllPagesHtmlAsync(cts.Token).ToListAsync().AsTask();

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
