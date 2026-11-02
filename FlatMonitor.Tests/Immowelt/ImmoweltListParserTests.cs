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
    public void ParseSearchData_NoListings_IsAnEmptyPage()
    {
        var page = _parser.ParseSearchData("""{"pageProps":{"totalCount":268,"classifieds":[],"classifiedsData":{}}}""");

        page.TotalCount.Should().Be(268);
        page.Flats.Should().BeEmpty();
    }
}
