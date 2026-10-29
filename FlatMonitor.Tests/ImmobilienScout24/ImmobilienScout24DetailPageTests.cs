namespace FlatMonitor.Tests.ImmobilienScout24;

using FlatMonitor.Models;
using FlatMonitor.Scraping.ImmobilienScout24;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmobilienScout24DetailPageTests
{
    private static string FixtureHtml() =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "ImmobilienScout24", "detail-page-sample.html"));

    private static Flat Card() => new()
    {
        ListingId = "123",
        Url = "https://www.immobilienscout24.de/expose/123",
        Title = "Card title",
        Address = "Card address",
        PostalCode = "76131",
        District = "Oststadt",
        PriceEur = 250000,
        RoomsCount = 3,
        LivingAreaSqm = 80,
        ScrapedAtUtc = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
        Source = ImmobilienScout24Scraper.SourceName,
    };

    [Test]
    public void Parse_SampleFixture_ReturnsEveryValueOfThePage()
    {
        var page = new ImmobilienScout24DetailParser().Parse(FixtureHtml());

        page.Title.Should().Be("Immobilienscout24 Detail Page Sample");
        page.OriginalPrice.Should().BeNull();
        page.Details!.PropertyType.Should().Be("Etagenwohnung");
        page.Details.BuildYear.Should().Be(1976);
        page.Metadata.Should().BeEquivalentTo(new ImmobilienScout24DetailParser.ExposeMetadata
        {
            Address = "Daxlanden, 76189 Karlsruhe",
            PostalCode = "76189",
            District = "Daxlanden",
            LivingAreaSqm = 104,
            RoomsCount = 4,
            PriceEur = 347000,
            CommissionPercent = 3.57,
        });
    }

    [Test]
    public void ParseDetailFields_ReturnsTheDetailsOfParse()
    {
        var parser = new ImmobilienScout24DetailParser();
        var html = FixtureHtml();

        parser.ParseDetailFields(html).Should().BeEquivalentTo(parser.Parse(html).Details);
    }

    [Test]
    public void ApplyTo_SampleFixture_TakesTheValuesFromThePageAndKeepsTheCardsIdentity()
    {
        var card = Card();
        var page = new ImmobilienScout24DetailParser().Parse(FixtureHtml());

        var flat = page.ApplyTo(card);

        flat.Should().BeEquivalentTo(card with
        {
            Title = "Immobilienscout24 Detail Page Sample",
            Address = "Daxlanden, 76189 Karlsruhe",
            PostalCode = "76189",
            District = "Daxlanden",
            PriceEur = 347000,
            OriginalPriceEur = 347000,
            RoomsCount = 4,
            LivingAreaSqm = 104,
            CommissionPercent = 3.57,
            Details = page.Details,
        });
    }

    [Test]
    public void ApplyTo_WithoutExposeMetadata_KeepsTheCardsValues()
    {
        // No metadata script: robot guard or changed markup; the card's values must not be cleared
        var page = new ImmobilienScout24DetailPage(Title: null, Metadata: null, OriginalPrice: null, Details: null);
        var card = Card() with { CommissionPercent = 3.57 };

        var flat = page.ApplyTo(card);

        flat.Should().Be(card with { OriginalPriceEur = card.PriceEur });
    }

    [Test]
    public void ApplyTo_WithPartialExposeMetadata_TakesThePagesValuesAndKeepsTheCardsForTheRest()
    {
        var page = new ImmobilienScout24DetailPage(
            Title: null,
            Metadata: new ImmobilienScout24DetailParser.ExposeMetadata { District = "Daxlanden", PriceEur = 299000 },
            OriginalPrice: null,
            Details: null);
        var card = Card();

        var flat = page.ApplyTo(card);

        flat.Should().Be(card with { District = "Daxlanden", PriceEur = 299000, OriginalPriceEur = 299000 });
    }

    [Test]
    public void ApplyTo_WithAStruckOutPrice_UsesItAsTheOriginalPrice()
    {
        var page = new ImmobilienScout24DetailPage(
            Title: null,
            Metadata: new ImmobilienScout24DetailParser.ExposeMetadata { PriceEur = 299000 },
            OriginalPrice: 320000,
            Details: null);

        var flat = page.ApplyTo(Card());

        flat.PriceEur.Should().Be(299000);
        flat.OriginalPriceEur.Should().Be(320000);
    }
}
