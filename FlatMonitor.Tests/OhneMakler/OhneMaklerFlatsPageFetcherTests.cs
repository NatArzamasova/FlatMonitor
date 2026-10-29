namespace FlatMonitor.Tests.OhneMakler;

using System.Net;
using NUnit.Framework;
using FluentAssertions;
using FlatMonitor.Scraping.OhneMakler;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class OhneMaklerFlatsPageFetcherTests
{
    private const string PageWithListings = "<html><body><a href='/immobilie/1/'>Flat</a></body></html>";
    private const string PageWithoutListings = "<html><body></body></html>";

    private static OhneMaklerFlatsPageFetcher CreateFetcher(Func<int, HttpResponseMessage> respond) =>
        new(new HttpClient(new PageHandler(respond)), NullLogger.Instance);

    private static HttpResponseMessage Html(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html) };

    [Test]
    public async Task FetchAllPagesHtmlAsync_StopsAtTheFirstPageWithoutListings()
    {
        // Arrange
        var fetcher = CreateFetcher(page => Html(page == 1 ? PageWithListings : PageWithoutListings));

        // Act
        var pages = await fetcher.FetchAllPagesHtmlAsync().ToListAsync();

        // Assert
        pages.Should().Equal(PageWithListings);
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenTheFirstPageTimesOut_Throws()
    {
        // Arrange: HttpClient reports a timeout as TaskCanceledException, with no canceled token
        var fetcher = CreateFetcher(_ => throw new TaskCanceledException("timeout", new TimeoutException()));

        // Act
        var act = () => fetcher.FetchAllPagesHtmlAsync().ToListAsync().AsTask();

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenTheFirstPageFails_Throws()
    {
        // Arrange
        var fetcher = CreateFetcher(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Act
        var act = () => fetcher.FetchAllPagesHtmlAsync().ToListAsync().AsTask();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenALaterPageFails_ThrowsAfterYieldingTheEarlierPages()
    {
        // Arrange
        var fetcher = CreateFetcher(page => page == 1 ? Html(PageWithListings) : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var pages = new List<string>();

        // Act
        var act = async () =>
        {
            await foreach (var page in fetcher.FetchAllPagesHtmlAsync())
                pages.Add(page);
        };

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        pages.Should().Equal(PageWithListings);
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenTheFirstPageHasNoListings_Throws()
    {
        // Arrange
        var fetcher = CreateFetcher(_ => Html(PageWithoutListings));

        // Act
        var act = () => fetcher.FetchAllPagesHtmlAsync().ToListAsync().AsTask();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No listings on the first page*");
    }

    [Test]
    public async Task FetchAllPagesHtmlAsync_WhenCanceled_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var fetcher = CreateFetcher(_ => Html(PageWithListings));

        // Act
        var act = () => fetcher.FetchAllPagesHtmlAsync(cts.Token).ToListAsync().AsTask();

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>Answers each listings page request with <c>respond(pageNumber)</c>.</summary>
    private sealed class PageHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var query = request.RequestUri!.Query;
            int page = query.StartsWith("?page=") ? int.Parse(query["?page=".Length..]) : 1;
            return Task.FromResult(respond(page));
        }
    }
}
