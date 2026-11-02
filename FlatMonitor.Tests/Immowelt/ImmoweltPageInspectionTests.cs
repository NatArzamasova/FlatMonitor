namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmoweltPageInspectionTests
{
    // Shortened from the 403 page immowelt sends to plain HTTP clients
    private const string DataDomeHtml = """
        <html lang="de"><head><title>immowelt.de</title></head><body style="margin:0">
        <p id="cmsg">Please enable JS and disable any ad blocker</p>
        <script data-cfasync="false">var dd={'rt':'c','host':'geo.captcha-delivery.com'}</script>
        <script data-cfasync="false" src="https://ct.captcha-delivery.com/c.js"></script></body></html>
        """;

    [Test]
    public void Inspect_DataDomeGuard_IsRecognized()
    {
        var inspection = ImmoweltPageInspection.Inspect(DataDomeHtml);

        inspection.IsDataDomeGuard.Should().BeTrue();
        inspection.ExposeUrls.Should().BeEmpty();
        inspection.DataMarkers.Should().BeEmpty();
        inspection.Bytes.Should().Be(DataDomeHtml.Length);
    }

    [Test]
    public void Inspect_ExposeLinks_AreAbsoluteDistinctAndWithoutQuery()
    {
        const string html = """
            <a href="/expose/2bz3k5x?foo=1">Wohnung</a>
            <a href="/expose/2bz3k5x">Wohnung (Bild)</a>
            <a href="https://www.immowelt.de/expose/abc123#top">Zweite</a>
            <a href="/classified-search?page=2">Weiter</a>
            """;

        var inspection = ImmoweltPageInspection.Inspect(html);

        inspection.IsDataDomeGuard.Should().BeFalse();
        inspection.ExposeUrls.Should().Equal(
            "https://www.immowelt.de/expose/2bz3k5x",
            "https://www.immowelt.de/expose/abc123");
    }

    [Test]
    public void Inspect_EmbeddedData_ListsTheMarkersFound()
    {
        const string html = """
            <script>window["__UFRN_LIFECYCLE_SERVERREQUEST__"]=JSON.parse("{}")</script>
            <script type="application/ld+json">{}</script>
            """;

        ImmoweltPageInspection.Inspect(html).DataMarkers
            .Should().Equal("__UFRN_LIFECYCLE_SERVERREQUEST__", "application/ld+json");
    }

    [Test]
    public void ToString_SummarizesThePage()
    {
        ImmoweltPageInspection.Inspect(DataDomeHtml).ToString()
            .Should().Be($"{DataDomeHtml.Length} bytes, DataDome guard: yes, expose links: 0, data blobs: none");
    }
}
