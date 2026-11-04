namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ImmoweltRobotCheckTests
{
    private const string Url = "https://www.immowelt.de/classified-search?distributionTypes=Buy";
    private const string DataDomePage = "<html><script src=\"https://geo.captcha-delivery.com/captcha/\"></script></html>";
    private const string DataPage = "<html>DATA</html>";
    private const string EmptyPage = "<html><body></body></html>";

    private static readonly PageGotoOptions GotoOptions = new() { WaitUntil = WaitUntilState.DOMContentLoaded };

    private Mock<IPage> _page = null!;
    private Queue<string> _contents = null!;
    private List<string> _openedUrls = null!;

    [SetUp]
    public void Setup()
    {
        _page = new Mock<IPage>(MockBehavior.Strict);
        _contents = new Queue<string>();
        _openedUrls = [];

        _page.Setup(p => p.IsClosed).Returns(false);
        _page.Setup(p => p.BringToFrontAsync()).Returns(Task.CompletedTask);
        // The last queued HTML stays in the tab
        _page.Setup(p => p.ContentAsync()).ReturnsAsync(() => _contents.Count > 1 ? _contents.Dequeue() : _contents.Peek());
        _page.Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .Callback((string url, PageGotoOptions? _) => _openedUrls.Add(url))
            .ReturnsAsync((IResponse?)null);
    }

    private ImmoweltRobotCheck CreateCheck(int waitSeconds = 10) =>
        new(_page.Object, new ImmoweltOptions { RobotCheckWaitSeconds = waitSeconds }, NullLogger.Instance, TimeSpan.FromMilliseconds(1));

    private static string? Parse(string html) => html == DataPage ? "parsed" : null;

    private void InTheTab(params string[] htmls)
    {
        foreach (var html in htmls)
            _contents.Enqueue(html);
    }

    [Test]
    public async Task WaitForPassAsync_WhenTheUserPassesTheCheck_ReturnsTheTabsDataAndHtml()
    {
        InTheTab(DataDomePage, DataDomePage, DataPage);

        var passed = await CreateCheck().WaitForPassAsync(Url, Parse, GotoOptions, default);

        passed.Should().Be(("parsed", DataPage));
        _page.Verify(p => p.BringToFrontAsync(), Times.Once);
        _openedUrls.Should().BeEmpty("the tab is only read, the check reloads the page itself");
    }

    [Test]
    public async Task WaitForPassAsync_WithWaitingOff_ReturnsNullWithoutTouchingTheTab()
    {
        var passed = await CreateCheck(waitSeconds: 0).WaitForPassAsync(Url, Parse, GotoOptions, default);

        passed.Should().BeNull();
        _page.Verify(p => p.ContentAsync(), Times.Never);
        _page.Verify(p => p.BringToFrontAsync(), Times.Never);
    }

    [Test]
    public async Task WaitForPassAsync_WhenTheCheckIsNotPassedInTime_ReturnsNull()
    {
        InTheTab(DataDomePage);

        var passed = await CreateCheck(waitSeconds: 1).WaitForPassAsync(Url, Parse, GotoOptions, default);

        passed.Should().BeNull();
    }

    [Test]
    public async Task WaitForPassAsync_WhenTheCheckIsGoneWithoutData_OpensTheUrlAgainOnce()
    {
        InTheTab(DataDomePage, EmptyPage, EmptyPage, DataPage);

        var passed = await CreateCheck().WaitForPassAsync(Url, Parse, GotoOptions, default);

        passed.Should().NotBeNull();
        _openedUrls.Should().Equal([Url], "the URL is opened again once, not on every read");
    }

    [Test]
    public async Task WaitForPassAsync_WhileTheTabIsNavigating_ReadsItAgain()
    {
        var reads = 0;
        _page.Setup(p => p.ContentAsync()).Returns(() => ++reads == 1
            ? Task.FromException<string>(new PlaywrightException("Execution context was destroyed"))
            : Task.FromResult(DataPage));

        var passed = await CreateCheck().WaitForPassAsync(Url, Parse, GotoOptions, default);

        passed.Should().NotBeNull();
    }

    [Test]
    public async Task WaitForPassAsync_WhenTheTabIsClosed_ReturnsNull()
    {
        _page.Setup(p => p.IsClosed).Returns(true);

        var passed = await CreateCheck().WaitForPassAsync(Url, Parse, GotoOptions, default);

        passed.Should().BeNull();
        _page.Verify(p => p.ContentAsync(), Times.Never);
    }

    [Test]
    public async Task WaitForPassAsync_WhenCanceled_Throws()
    {
        InTheTab(DataDomePage);

        var act = () => CreateCheck().WaitForPassAsync(Url, Parse, GotoOptions, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
