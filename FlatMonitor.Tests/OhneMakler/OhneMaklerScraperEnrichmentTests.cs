namespace FlatMonitor.Tests.OhneMakler;

using System.Net;
using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Models;
using FlatMonitor.Scraping.OhneMakler;
using FlatMonitor.Storage;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Runs <see cref="OhneMaklerScraper"/> end to end against the saved fixtures, served by a fake HTTP handler:
/// the listings page for page 1, an empty page 2, and the detail page for every listing except <see cref="FailingId"/>.
/// </summary>
[TestFixture]
public class OhneMaklerScraperEnrichmentTests
{
    private const string FailingId = "502815";

    private string _dataDir = null!;
    private List<Flat> _cards = null!;
    private Dictionary<string, Flat> _scraped = null!;

    [OneTimeSetUp]
    public async Task ScrapeFixtures()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"OhneMaklerScraperEnrichmentTests_{Guid.NewGuid()}");
        var listingsHtml = await File.ReadAllTextAsync(FixturePath("listings-page-sample.html"));
        var detailHtml = await File.ReadAllTextAsync(FixturePath("detail-page-sample.html"));

        _cards = new OhneMaklerFlatCardParser().ParsePage(listingsHtml).Select(c => c.Flat).ToList();

        using var httpClient = new HttpClient(new FixtureHandler(listingsHtml, detailHtml));
        var scraper = new OhneMaklerScraper(
            httpClient, new StorageOptions(_dataDir, "Data Source=:memory:"), NullLogger<OhneMaklerScraper>.Instance);
        _scraped = (await scraper.ScrapeAsync().ToListAsync()).ToDictionary(f => f.ListingId);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // The scraper saves detail HTML in the background; a write still in progress may keep the folder busy
        }
    }

    [Test]
    public void Fixture_HasOneFailingAndAtLeastOneWorkingDetailPage()
    {
        _scraped.Keys.Should().Contain(FailingId).And.HaveCountGreaterThan(1);
    }

    [Test]
    public void EnrichedFlat_TakesDetailPageDataAndKeepsCardFieldsItDoesNotReplace()
    {
        foreach (var card in _cards.Where(c => c.ListingId != FailingId && _scraped.ContainsKey(c.ListingId)))
        {
            var flat = _scraped[card.ListingId];

            flat.Url.Should().StartWith("https://").And.EndWith(card.Url);
            flat.Details.Should().NotBeNull();
            flat.OriginalPriceEur.Should().Be(flat.PriceEur);
            flat.Source.Should().Be(OhneMaklerScraper.SourceName);
            flat.CommissionPercent.Should().Be(0, "ohne-makler.net has no estate agent");

            // Card fields that the detail page does not replace
            flat.PostalCode.Should().Be(card.PostalCode);
            flat.District.Should().Be(card.District);
            flat.DetailPageUrl.Should().Be(card.DetailPageUrl);
        }
    }

    [Test]
    public void FlatWhoseDetailPageFails_KeepsCardDataWithAbsoluteUrl()
    {
        var card = _cards.Single(c => c.ListingId == FailingId);

        // Same as the card, apart from the absolute URL and the original price defaulting to the price
        // (ScrapedAtUtc differs only because the test parses the cards a second time)
        _scraped[FailingId].Should().Be(card with
        {
            Url = _scraped[FailingId].Url,
            OriginalPriceEur = card.PriceEur,
            ScrapedAtUtc = _scraped[FailingId].ScrapedAtUtc,
        });
        _scraped[FailingId].Url.Should().StartWith("https://").And.EndWith(card.Url);
    }

    private static string FixturePath(string filename) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "OhneMakler", filename);

    private sealed class FixtureHandler(string listingsHtml, string detailHtml) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var response = uri.AbsolutePath.Contains($"/immobilie/{FailingId}/")
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        uri.AbsolutePath.Contains("/immobilie/") ? detailHtml
                        : uri.Query.Contains("page=") ? "<html><body></body></html>"
                        : listingsHtml),
                };
            return Task.FromResult(response);
        }
    }
}
