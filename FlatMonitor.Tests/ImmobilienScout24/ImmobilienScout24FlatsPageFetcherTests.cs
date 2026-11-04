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

    private const string BannerTimeout =
        "Timeout 10000ms exceeded. <div id=\"usercentrics-root\"></div> intercepts pointer events";

    /// <summary>A "next" button whose first <paramref name="failures"/> clicks time out.</summary>
    private Mock<IElementHandle> AddBlockedNextButton(int failures, string message = BannerTimeout)
    {
        var button = new Mock<IElementHandle>();
        var clicks = 0;
        button.Setup(b => b.ClickAsync(It.IsAny<ElementHandleClickOptions>()))
            .Returns(() => ++clicks <= failures ? Task.FromException(new TimeoutException(message)) : Task.CompletedTask);
        _nextButtons.Enqueue(button.Object);
        return button;
    }

    private Mock<IElementHandle> SetupDenyButton()
    {
        var denyButton = new Mock<IElementHandle>();
        denyButton.Setup(b => b.ClickAsync(It.IsAny<ElementHandleClickOptions>())).Returns(Task.CompletedTask);
        _page.Setup(p => p.QuerySelectorAsync(ImmobilienScout24Selectors.CookieBannerDenyButtonCss, It.IsAny<PageQuerySelectorOptions>()))
            .ReturnsAsync(denyButton.Object);
        return denyButton;
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_ClicksNextWithAShortTimeout()
    {
        // Arrange
        var button = AddBlockedNextButton(failures: 0);

        // Act
        await CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync();

        // Assert
        button.Verify(b => b.ClickAsync(It.Is<ElementHandleClickOptions>(
            o => o.Timeout == ImmobilienScout24FlatsPageFetcher.NextButtonClickTimeoutMs)), Times.Once);
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenTheCookieBannerCoversNext_DeniesTheCookiesAndClicksAgain()
    {
        // Arrange
        var button = AddBlockedNextButton(failures: 1);
        var denyButton = SetupDenyButton();

        // Act
        var pages = await CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync();

        // Assert
        pages.Should().Equal(PageWithListings);
        denyButton.Verify(b => b.ClickAsync(It.IsAny<ElementHandleClickOptions>()), Times.Once);
        button.Verify(b => b.ClickAsync(It.IsAny<ElementHandleClickOptions>()), Times.Exactly(2));
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenNextCannotBeClickedAndThereIsNoBanner_ThrowsAShortMessage()
    {
        // Arrange
        AddBlockedNextButton(failures: 1, message: "Timeout 10000ms exceeded. <div class=\"overlay\"></div> intercepts pointer events");

        // Act
        var act = () => CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync().AsTask();

        // Assert
        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.WithMessage("ImmobilienScout24 page 2: the next page button cannot be clicked: something covers it*");
        thrown.Which.InnerException.Should().BeNull("Playwright's call log lists every retry");
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenTheBannerStillCoversNextAfterDenying_ThrowsAShortMessage()
    {
        // Arrange
        AddBlockedNextButton(failures: 2);
        SetupDenyButton();

        // Act
        var act = () => CreateFetcher().FetchAllPagesHtmlAsync().ToListAsync().AsTask();

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*page 2*cannot be clicked*cookie banner (usercentrics-root)*");
    }
}
