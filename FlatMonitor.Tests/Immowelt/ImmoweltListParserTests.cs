namespace FlatMonitor.Tests.Immowelt;

using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmoweltListParserTests
{
    private ImmoweltListParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ImmoweltListParser();
    }

    private ImmoweltListPage ParseFixture() => _parser.Parse(ImmoweltFixtures.Read("list-page.html"))!;

    [Test]
    public void Parse_ListPage_ReadsTheTotalCount()
    {
        ParseFixture().TotalCount.Should().Be(268);
    }

    [Test]
    public void Parse_ListPage_ReadsThePagesSalesInOrder()
    {
        var flats = ParseFixture().Flats;

        // 30 listings, one of them a compulsory auction
        flats.Should().HaveCount(29);
        flats.First().ListingId.Should().Be("25NJ8GP9B1XE");
        flats.Last().ListingId.Should().Be("26PXSZFU9VT8");
        flats.Select(f => f.ListingId).Should().OnlyHaveUniqueItems();
        flats.Should().OnlyContain(f => f.Source == "Immowelt");
    }

    [Test]
    public void Parse_ListPage_LeavesOutSuggestionsOutsideTheSearch()
    {
        ParseFixture().Flats.Select(f => f.ListingId)
            .Should().NotContain(new[] { "26EC8NT916JG", "263U1B1CRASJ", "26T9P6ZR76IZ" });
    }

    [Test]
    public void Parse_ListPage_LeavesOutAuctions()
    {
        ParseFixture().Flats.Select(f => f.ListingId).Should().NotContain("26ECIA8VQ4A2");
    }

    [Test]
    public void Parse_ListPage_ReadsAFlat()
    {
        var flat = ParseFixture().Flats.Single(f => f.ListingId == "25NJ8GP9B1XE");

        flat.Url.Should().Be("https://www.immowelt.de/expose/f598b7d0-bdcc-4066-b821-0dadc40dc626");
        flat.Title.Should().Be("Exklusive Erdgeschosswohnung mit Garten & Terrasse - Stilvoll Wohnen im Herzen der Stadt");
        flat.Address.Should().Be("76133 Karlsruhe");
        flat.PostalCode.Should().Be("76133");
        flat.District.Should().Be("Innenstadt-West");
        flat.PriceEur.Should().Be(549900);
        flat.OriginalPriceEur.Should().Be(549900);
        flat.RoomsCount.Should().Be(2);
        flat.LivingAreaSqm.Should().Be(82.09);
        flat.CommissionPercent.Should().BeNull("the list's brokerage fee flag is unreliable, the expose tells");
        flat.ScrapedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    public void Parse_ReducedPrice_KeepsThePreviousPriceAsOriginal()
    {
        var flat = ParseFixture().Flats.Single(f => f.ListingId == "25JF85U4L1XX");

        flat.PriceEur.Should().Be(275000);
        flat.OriginalPriceEur.Should().Be(295000);
    }

    [Test]
    public void Parse_PublishedStreet_IsPartOfTheAddress()
    {
        var flat = ParseFixture().Flats.Single(f => f.ListingId == "25JF85U4L1XX");

        flat.Address.Should().Be("Yorckstraße 49, 76135 Karlsruhe");
        flat.District.Should().Be("Mühlburg");
    }

    [Test]
    public void Parse_ListingWithoutPrice_IsKeptWithoutPrice()
    {
        var flat = ParseFixture().Flats.Single(f => f.ListingId == "24G7FBBFE1MM");

        flat.PriceEur.Should().BeNull();
        flat.OriginalPriceEur.Should().BeNull();
        flat.RoomsCount.Should().Be(3);
        flat.LivingAreaSqm.Should().Be(81);
    }

    [Test]
    public void Parse_PageWithoutSearchData_IsNull()
    {
        const string dataDome = """<html><body><p id="cmsg">Please enable JS and disable any ad blocker</p></body></html>""";

        _parser.Parse(dataDome).Should().BeNull();
    }

    [Test]
    public void ParseSearchData_ListingWithoutData_IsSkipped()
    {
        const string json = """
            {"pageProps":{"totalCount":2,"classifieds":["A1","B2"],"classifiedsData":{
              "B2":{"id":"B2","url":"https://www.immowelt.de/expose/b2","rawData":{"distributionType":"BUY","price":100000}}}}}
            """;

        var page = _parser.ParseSearchData(json);

        page.TotalCount.Should().Be(2);
        page.Flats.Should().ContainSingle().Which.ListingId.Should().Be("B2");
    }

    [Test]
    public void ParseSearchData_ListingWithMissingFields_KeepsWhatItHas()
    {
        const string json = """
            {"pageProps":{"totalCount":1,"classifieds":["A1"],"classifiedsData":{
              "A1":{"id":"A1","rawData":{"distributionType":"BUY"},"hardFacts":{"title":"Wohnung zum Kauf"}}}}}
            """;

        var flat = _parser.ParseSearchData(json).Flats.Single();

        flat.ListingId.Should().Be("A1");
        flat.Title.Should().Be("Wohnung zum Kauf");
        flat.Url.Should().BeNull();
        flat.Address.Should().BeNull();
        flat.District.Should().BeNull();
        flat.PriceEur.Should().BeNull();
        flat.RoomsCount.Should().BeNull();
    }

    [Test]
    public void Parse_ListPage_UrlIsTheExposeOfTheLegacyId()
    {
        // On a single flat's card, url and legacyId name the same expose
        ParseFixture().Flats.Should().OnlyContain(flat => flat.Url!.StartsWith("https://www.immowelt.de/expose/"));
    }

    [Test]
    public void ParseSearchData_ProjectUnits_GetTheirOwnExposeInsteadOfTheProjectPage()
    {
        // Two units of a new-build project (2026-11-02): the card links both to the shared project page
        const string json = """
            {"pageProps":{"totalCount":2,"classifieds":["26P21WB1A3G8","26HPTXRS8IMV"],"classifiedsData":{
              "26P21WB1A3G8":{"id":"26P21WB1A3G8","url":"https://www.immowelt.de/projekte/expose/2628ew8d2t3t",
                "metadata":{"legacyId":"1e0b3367-d1de-4726-97e4-1cbaaf7233a5"},"rawData":{"distributionType":"BUY","price":419000}},
              "26HPTXRS8IMV":{"id":"26HPTXRS8IMV","url":"https://www.immowelt.de/projekte/expose/2628ew8d2t3t",
                "metadata":{"legacyId":"78f5e1e9-1b8f-47df-a0c9-695ab45f2284"},"rawData":{"distributionType":"BUY","price":449000}}}}}
            """;

        var flats = _parser.ParseSearchData(json).Flats;

        flats.Select(f => f.Url).Should().Equal(
            "https://www.immowelt.de/expose/1e0b3367-d1de-4726-97e4-1cbaaf7233a5",
            "https://www.immowelt.de/expose/78f5e1e9-1b8f-47df-a0c9-695ab45f2284");
    }

    [TestCase(null, TestName = "ParseSearchData_ProjectUnitWithoutLegacyId_KeepsTheProjectPage")]
    [TestCase("9324", TestName = "ParseSearchData_ProjectUnitWithANonGuidLegacyId_KeepsTheProjectPage")]
    public void ParseSearchData_ProjectUnitWithoutExposeId_KeepsTheProjectPage(string? legacyId)
    {
        var metadata = legacyId == null ? "{}" : """{"legacyId":"ID"}""".Replace("ID", legacyId);
        var json = """
            {"pageProps":{"totalCount":1,"classifieds":["A1"],"classifiedsData":{
              "A1":{"id":"A1","url":"https://www.immowelt.de/projekte/expose/2628ew8d2t3t","metadata":METADATA,
                "rawData":{"distributionType":"BUY"}}}}}
            """.Replace("METADATA", metadata);

        _parser.ParseSearchData(json).Flats.Single().Url.Should().Be("https://www.immowelt.de/projekte/expose/2628ew8d2t3t");
    }

    [Test]
    public void ParseSearchData_NoListings_IsAnEmptyPage()
    {
        var page = _parser.ParseSearchData("""{"pageProps":{"totalCount":268,"classifieds":[],"classifiedsData":{}}}""");

        page.TotalCount.Should().Be(268);
        page.Flats.Should().BeEmpty();
    }

    [Test]
    public void Parse_ListPage_ReadsTheFirstGalleryPhotoAtPreviewSize()
    {
        var flat = ParseFixture().Flats.Single(f => f.ListingId == "25NJ8GP9B1XE");

        flat.ImageUrl.Should().Be("https://mms.immowelt.de/b/f/7/8/bf7892eb-b5e0-47bc-b429-92ccd2e2c2a3.jpg?w=400&h=300");
    }

    [Test]
    public void Parse_ListPage_GivesMostFlatsAPhoto()
    {
        ParseFixture().Flats.Should().OnlyContain(f => f.ImageUrl == null || f.ImageUrl.StartsWith("https://mms.immowelt.de/"))
            .And.Contain(f => f.ImageUrl != null);
    }

    [TestCase("""{"gallery":{"images":[{"url":"https://mms.immowelt.de/a/b.jpg?w=626&h=470"},{"url":"https://mms.immowelt.de/c/d.jpg"}]}}""",
        "https://mms.immowelt.de/a/b.jpg?w=400&h=300")]
    [TestCase("""{"gallery":{"images":[{"key":"no-url"},{"url":"https://mms.immowelt.de/c/d.jpg"}]}}""",
        "https://mms.immowelt.de/c/d.jpg?w=400&h=300")]
    [TestCase("""{"gallery":{"images":[]}}""", null)]
    [TestCase("""{"gallery":{"images":[{"url":"http://mms.immowelt.de/a/b.jpg"}]}}""", null)]
    [TestCase("""{"gallery":{"images":[{"url":"/a/b.jpg"}]}}""", null)]
    [TestCase("""{}""", null)]
    public void PreviewImageUrl_TakesTheFirstHttpsPhotoAt400x300(string classifiedJson, string? expected)
    {
        using var classified = System.Text.Json.JsonDocument.Parse(classifiedJson);

        ImmoweltListParser.PreviewImageUrl(classified.RootElement).Should().Be(expected);
    }
}
