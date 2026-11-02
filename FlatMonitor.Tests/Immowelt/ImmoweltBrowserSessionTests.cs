namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;

[TestFixture]
public class ImmoweltBrowserSessionTests
{
    private Mock<IPlaywright> _playwright = null!;
    private Mock<IPage> _page = null!;

    [SetUp]
    public void Setup()
    {
        _playwright = new Mock<IPlaywright>();
        _page = new Mock<IPage>();
    }

    [Test]
    public async Task DisposeAsync_ClosesTheTabAndDisconnects()
    {
        await new ImmoweltBrowserSession(_playwright.Object, _page.Object).DisposeAsync();

        _page.Verify(p => p.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Once);
        _playwright.Verify(p => p.Dispose(), Times.Once);
    }

    [Test]
    public async Task DisposeAsync_ConnectionAlreadyLost_DoesNotFailTheRun()
    {
        // Windows' sleep cut the connection: closing the tab fails as a closed target
        var lost = new PlaywrightException("Target page, context or browser has been closed");
        _page.Setup(p => p.CloseAsync(It.IsAny<PageCloseOptions>())).ThrowsAsync(lost);
        var session = new ImmoweltBrowserSession(_playwright.Object, _page.Object, Mock.Of<IBrowser>());

        var act = async () => await session.DisposeAsync();

        await act.Should().NotThrowAsync();
        _playwright.Verify(p => p.Dispose(), Times.Once);
    }
}
