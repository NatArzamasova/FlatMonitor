namespace FlatMonitor.Tests.Immowelt;

using System.Net;
using System.Net.Sockets;
using FlatMonitor.Scraping.Immowelt;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

/// <summary>A run needs the user's Chrome: here only the run without one. The live test covers a real run.</summary>
[TestFixture]
public class ImmoweltScraperTests
{
    private string _dir = null!;

    [SetUp]
    public void Setup() => _dir = Path.Combine(Path.GetTempPath(), $"ImmoweltScraperTests_{Guid.NewGuid()}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Test]
    public void Source_IsImmowelt()
    {
        CreateScraper(new ImmoweltOptions()).Source.Should().Be("Immowelt");
    }

    [Test]
    public async Task ScrapeAsync_WithoutChromeAtTheCdpUrl_FailsAtStartAndSaysHowToStartIt()
    {
        var scraper = CreateScraper(new ImmoweltOptions { CdpUrl = $"http://127.0.0.1:{FreePort()}" });

        var act = () => scraper.ScrapeAsync().ToListAsync().AsTask();

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("No Chrome to attach to*");
    }

    private ImmoweltScraper CreateScraper(ImmoweltOptions options) =>
        new(new StorageOptions(_dir, $"Data Source={Path.Combine(_dir, "test.db")}"), options, NullLogger<ImmoweltScraper>.Instance);

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
