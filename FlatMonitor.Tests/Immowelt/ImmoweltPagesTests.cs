namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmoweltPagesTests
{
    private const string SearchUrl = "https://www.immowelt.de/classified-search?distributionTypes=Buy&estateTypes=Apartment&locations=AD08DE5585";

    [Test]
    public void PageUrl_FirstPage_IsTheSearchUrl()
    {
        ImmoweltPages.PageUrl(SearchUrl, 1).Should().Be(SearchUrl);
    }

    [Test]
    public void PageUrl_LaterPage_AddsThePageParameter()
    {
        ImmoweltPages.PageUrl(SearchUrl, 3).Should().Be(SearchUrl + "&page=3");
    }

    [TestCase("https://www.immowelt.de/classified-search?locations=AD08DE5585&page=2", 4,
        "https://www.immowelt.de/classified-search?locations=AD08DE5585&page=4", TestName = "PageUrl_ReplacesAPageParameterAtTheEnd")]
    [TestCase("https://www.immowelt.de/classified-search?page=2&locations=AD08DE5585", 4,
        "https://www.immowelt.de/classified-search?locations=AD08DE5585&page=4", TestName = "PageUrl_ReplacesAPageParameterAtTheStart")]
    [TestCase("https://www.immowelt.de/classified-search?page=2", 1,
        "https://www.immowelt.de/classified-search", TestName = "PageUrl_FirstPage_DropsThePageParameter")]
    [TestCase("https://www.immowelt.de/classified-search", 2,
        "https://www.immowelt.de/classified-search?page=2", TestName = "PageUrl_UrlWithoutQuery_StartsTheQuery")]
    public void PageUrl_ExistingPageParameter(string searchUrl, int page, string expected)
    {
        ImmoweltPages.PageUrl(searchUrl, page).Should().Be(expected);
    }

    [Test]
    public void PageUrl_PageBelowOne_Throws()
    {
        var act = () => ImmoweltPages.PageUrl(SearchUrl, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ReadDataScript_UnescapesTheJsonText()
    {
        const string html = """
            <script>window["__OTHER__"]=JSON.parse("{\"a\":1}");</script>
            <script id="x">window["__UFRN_FETCHER__"]=JSON.parse("{\"text\":\"Grötzingen \\\"Süd\\\"\",\"url\":\"<\/script>\"}");</script>
            """;

        ImmoweltPages.ReadDataScript(html, "__UFRN_FETCHER__")
            .Should().Be("""{"text":"Grötzingen \"Süd\"","url":"</script>"}""");
    }

    [Test]
    public void ReadDataScript_PageWithoutTheScript_IsNull()
    {
        ImmoweltPages.ReadDataScript("<html><body>Please enable JS</body></html>", ImmoweltPages.ListDataScript)
            .Should().BeNull();
    }

    [Test]
    public void ReadDataScript_ExposeFixture_IsJson()
    {
        var json = ImmoweltPages.ReadDataScript(ImmoweltFixtures.Read("expose-page.html"), ImmoweltPages.ExposeDataScript);

        json.Should().StartWith("""{"app_demand_referral_cldp":""");
    }
}
