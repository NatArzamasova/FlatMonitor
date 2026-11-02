namespace FlatMonitor.Tests.Immowelt;

using System.Net;
using System.Net.Sockets;
using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

/// <summary>
/// The attach failures. Attaching to a real Chrome needs one started by hand: covered by the live test.
/// </summary>
[TestFixture]
public class ImmoweltBrowserTests
{
    [Test]
    public async Task AttachAsync_WithoutChromeAtTheCdpUrl_TellsHowToStartIt()
    {
        var cdpUrl = $"http://127.0.0.1:{FreePort()}";
        var browser = new ImmoweltBrowser(new ImmoweltOptions { CdpUrl = cdpUrl }, NullLogger.Instance);

        var act = () => browser.AttachAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"No Chrome to attach to at {cdpUrl}*--remote-debugging-port=*robot check*");
    }

    [Test]
    public async Task AttachAsync_WhenCanceled_DoesNotConnect()
    {
        var browser = new ImmoweltBrowser(new ImmoweltOptions(), NullLogger.Instance);

        var act = () => browser.AttachAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestCase("http://127.0.0.1:9222", "--remote-debugging-port=9222 ")]
    [TestCase("http://127.0.0.1:9333", "--remote-debugging-port=9333 ")]
    public void NoChromeMessage_NamesThePortOfTheCdpUrl(string cdpUrl, string expected)
    {
        ImmoweltBrowser.NoChromeMessage(cdpUrl).Should().Contain(expected).And.Contain("--user-data-dir=");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
