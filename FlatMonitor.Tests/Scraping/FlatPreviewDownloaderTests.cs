using System.Net;
using System.Net.Http.Headers;
using FlatMonitor.Models;
using FlatMonitor.Scraping;
using FlatMonitor.Storage;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace FlatMonitor.Tests.Scraping;

[TestFixture]
public class FlatPreviewDownloaderTests
{
    private const string Source = "ImmobilienScout24";

    private Mock<IFlatPreviewRepository> _repository = null!;
    private Dictionary<string, string> _storedUrls = null!;
    private List<FlatPreview> _saved = null!;
    private StubHandler _handler = null!;
    private ListLogger<FlatPreviewDownloader> _logger = null!;
    private FlatPreviewDownloader _downloader = null!;
    private readonly ScrapeDatabaseLock _database = new();

    [SetUp]
    public void SetUp()
    {
        _storedUrls = [];
        _saved = [];
        _repository = new Mock<IFlatPreviewRepository>();
        _repository.Setup(r => r.GetImageUrlsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyCollection<string> ids, CancellationToken _) =>
                _storedUrls.Where(entry => ids.Contains(entry.Key)).ToDictionary());
        _repository.Setup(r => r.SaveAsync(It.IsAny<IReadOnlyCollection<FlatPreview>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<FlatPreview>, CancellationToken>((previews, _) => _saved.AddRange(previews))
            .Returns(Task.CompletedTask);

        _handler = new StubHandler();
        _logger = new ListLogger<FlatPreviewDownloader>();
        _downloader = new FlatPreviewDownloader(new HttpClient(_handler), _repository.Object, _logger);
    }

    [TearDown]
    public void TearDown() => _handler.Dispose();

    private static Flat Flat(string id, string? imageUrl = null) => new()
    {
        Source = Source,
        ListingId = id,
        ImageUrl = imageUrl ?? $"https://pictures.example/{id}.jpg",
        ScrapedAtUtc = DateTime.UtcNow,
    };

    [Test]
    public async Task SavePreviewsAsync_DownloadsAndStoresEachFlatsPhoto()
    {
        _handler.Respond("https://pictures.example/1.jpg", [1, 1], "image/jpeg");
        _handler.Respond("https://pictures.example/2.jpg", [2], "image/webp");

        var saved = await _downloader.SavePreviewsAsync([Flat("1"), Flat("2")], _database);

        saved.Should().Be(2);
        _saved.Should().BeEquivalentTo([
            new { Source, ListingId = "1", ImageUrl = "https://pictures.example/1.jpg", ContentType = "image/jpeg", Data = new byte[] { 1, 1 } },
            new { Source, ListingId = "2", ImageUrl = "https://pictures.example/2.jpg", ContentType = "image/webp", Data = new byte[] { 2 } },
        ]);
        _saved.Should().OnlyContain(p => p.DownloadedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Test]
    public async Task SavePreviewsAsync_SkipsFlatsWhosePreviewHasTheSameUrlAndReplacesAChangedOne()
    {
        _storedUrls["1"] = "https://pictures.example/1.jpg";
        _storedUrls["2"] = "https://pictures.example/2-old.jpg";
        _handler.Respond("https://pictures.example/2.jpg", [2], "image/jpeg");

        await _downloader.SavePreviewsAsync([Flat("1"), Flat("2")], _database);

        _handler.Requested.Should().Equal("https://pictures.example/2.jpg");
        _saved.Select(p => p.ListingId).Should().Equal("2");
    }

    [Test]
    public async Task SavePreviewsAsync_FlatsWithoutPhoto_AreLeftOutWithoutAnyRequest()
    {
        var saved = await _downloader.SavePreviewsAsync([Flat("1") with { ImageUrl = null }, Flat("2") with { ImageUrl = "" }], _database);

        saved.Should().Be(0);
        _handler.Requested.Should().BeEmpty();
        _repository.Verify(r => r.SaveAsync(It.IsAny<IReadOnlyCollection<FlatPreview>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SavePreviewsAsync_AFailingPhoto_LeavesOnlyThatFlatWithoutPreviewAndWarnsOnce()
    {
        _handler.Respond("https://pictures.example/1.jpg", [1], "image/jpeg");
        _handler.Respond("https://pictures.example/2.jpg", [], "text/html", HttpStatusCode.NotFound);
        _handler.Fail("https://pictures.example/3.jpg", new HttpRequestException("connection refused"));
        _handler.Respond("https://pictures.example/4.jpg", [4], "image/jpeg");

        var saved = await _downloader.SavePreviewsAsync([Flat("1"), Flat("2"), Flat("3"), Flat("4")], _database);

        saved.Should().Be(2);
        _saved.Select(p => p.ListingId).Should().BeEquivalentTo(["1", "4"]);
        _logger.Entries.Where(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning).Should().ContainSingle()
            .Which.Message.Should().Contain("2 of 4").And.Contain("2 failed");
    }

    [TestCase("image/svg+xml")]
    [TestCase("text/html")]
    [TestCase(null)]
    public async Task SavePreviewsAsync_AResponseThatIsNoPhoto_IsNotStored(string? contentType)
    {
        // The API serves the bytes from its own origin: an SVG or HTML could run script there
        _handler.Respond("https://pictures.example/1.jpg", [1], contentType);

        (await _downloader.SavePreviewsAsync([Flat("1")], _database)).Should().Be(0);

        _saved.Should().BeEmpty();
    }

    [Test]
    public async Task SavePreviewsAsync_APhotoLargerThanAPreview_IsNotStored()
    {
        _handler.Respond("https://pictures.example/1.jpg", new byte[FlatPreview.MaxBytes + 1], "image/jpeg");
        _handler.Respond("https://pictures.example/2.jpg", new byte[FlatPreview.MaxBytes], "image/jpeg", omitContentLength: true);
        _handler.Respond("https://pictures.example/3.jpg", new byte[FlatPreview.MaxBytes + 1], "image/jpeg", omitContentLength: true);

        await _downloader.SavePreviewsAsync([Flat("1"), Flat("2"), Flat("3")], _database);

        _saved.Select(p => p.ListingId).Should().Equal("2");
    }

    [Test]
    public async Task SavePreviewsAsync_TheSameFlatTwice_IsDownloadedOnce()
    {
        _handler.Respond("https://pictures.example/1.jpg", [1], "image/jpeg");

        await _downloader.SavePreviewsAsync([Flat("1"), Flat("1")], _database);

        _handler.Requested.Should().ContainSingle();
    }

    [Test]
    public async Task SavePreviewsAsync_WhenTheDatabaseFails_ReturnsZeroInsteadOfThrowing()
    {
        // A flat without preview is still a flat: the scrape goes on
        _handler.Respond("https://pictures.example/1.jpg", [1], "image/jpeg");
        _repository.Setup(r => r.SaveAsync(It.IsAny<IReadOnlyCollection<FlatPreview>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is locked"));

        (await _downloader.SavePreviewsAsync([Flat("1")], _database)).Should().Be(0);

        _logger.Entries.Should().Contain(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Test]
    public async Task SavePreviewsAsync_CanceledByTheCaller_Throws()
    {
        using var cts = new CancellationTokenSource();
        _handler.OnRequest = cts.Cancel;
        _handler.Respond("https://pictures.example/1.jpg", [1], "image/jpeg");

        var act = () => _downloader.SavePreviewsAsync([Flat("1")], _database, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// The picture servers as the parsers address them; the photos (from 2026-11-03) disappear with their listings,
    /// so replace a URL that 404s with a current one.
    /// </summary>
    [Test]
    [Explicit("Downloads photos from the portals' picture servers; run with: dotnet test --filter \"TestCategory=Live\"")]
    [Category("Live")]
    public async Task SavePreviewsAsync_Live_DownloadsAPhotoOfPreviewSizeFromEachPortal()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        var downloader = new FlatPreviewDownloader(httpClient, _repository.Object, _logger);
        Flat[] flats =
        [
            // Redirects to a signed media.ohne-makler.net URL
            Flat("om") with { ImageUrl = "https://www.ohne-makler.net/immobilie/426222/picture/0/medium.jpg" },
            Flat("is24") with { ImageUrl = "https://pictures.immobilienscout24.de/listings/8e456bab-c9b2-4d34-940c-ca4b1a87e73c-1667807503.jpg/ORIG/resize/400x300%3E/format/webp/quality/73" },
            Flat("iw") with { ImageUrl = "https://mms.immowelt.de/a/d/c/5/adc58dcd-36dc-44ea-9c75-8859df8a4380.jpg?w=400&h=300" },
        ];

        var saved = await downloader.SavePreviewsAsync(flats, _database);

        saved.Should().Be(3, string.Join(Environment.NewLine, _logger.Messages));
        foreach (var preview in _saved)
        {
            TestContext.Out.WriteLine($"{preview.ListingId}: {preview.ContentType}, {preview.Data.Length} bytes");
            preview.Data.Length.Should().BeInRange(2_000, 100_000);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];
        private readonly List<string> _requested = [];

        public Action? OnRequest { get; set; }

        public IReadOnlyList<string> Requested
        {
            get { lock (_requested) return [.. _requested]; }
        }

        public void Respond(string url, byte[] body, string? contentType, HttpStatusCode status = HttpStatusCode.OK, bool omitContentLength = false) =>
            _responses[url] = () =>
            {
                HttpContent content = omitContentLength
                    ? new StreamContent(new NonSeekableStream(body))
                    : new ByteArrayContent(body);
                if (contentType != null)
                    content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                return new HttpResponseMessage(status) { Content = content };
            };

        public void Fail(string url, Exception exception) => _responses[url] = () => throw exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            lock (_requested)
                _requested.Add(url);
            OnRequest?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return _responses.TryGetValue(url, out var respond)
                ? Task.FromResult(respond())
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>A body whose length the content cannot tell, as a chunked response.</summary>
    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
