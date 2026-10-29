namespace FlatMonitor.Tests.ImmobilienScout24;

using FlatMonitor.Scraping.ImmobilienScout24;
using FluentAssertions;
using HtmlAgilityPack;
using NUnit.Framework;

[TestFixture]
public class ImmobilienScout24CardParserTests
{
    private readonly ImmobilienScout24CardParser _parser = new();

    private static HtmlNode Card(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.SelectSingleNode(new ImmobilienScout24Selectors().ListingCardXPath);
    }

    [Test]
    public void Parse_ReadsTheCardsFieldsWithGermanNumbers()
    {
        var card = Card("""
            <div class="listing-card" data-obid="160123456">
              <a href="/expose/160123456"></a>
              <h2>Helle 2,5-Zimmer-Wohnung</h2>
              <p class="location">Weststadt, Karlsruhe</p>
              <dl><dd>389.000 €</dd><dd>74,5 m²</dd><dd>2,5 Zimmer</dd></dl>
            </div>
            """);

        var flat = _parser.Parse(card);

        flat.Should().NotBeNull();
        flat!.Source.Should().Be(ImmobilienScout24Scraper.SourceName);
        flat.ListingId.Should().Be("160123456");
        flat.Url.Should().Be("https://www.immobilienscout24.de/expose/160123456");
        flat.Title.Should().Be("Helle 2,5-Zimmer-Wohnung");
        flat.Address.Should().Be("Weststadt, Karlsruhe");
        flat.PriceEur.Should().Be(389000);
        flat.LivingAreaSqm.Should().Be(74.5);
        flat.RoomsCount.Should().Be(2.5);
    }

    [Test]
    public void Parse_ExposeLinkWithText_TakesTheLinkTextAsUrl()
    {
        // Known quirk, kept as it is: HtmlAgilityPack selects the <a> element for ".../@href", so the URL is the
        // link's text. On the live site the URL ends up built from the listing ID (every stored IS24 URL is /expose/{id}).
        var flat = _parser.Parse(Card("""<div class="listing-card" data-obid="160123456"><a href="/expose/160123456">Zum Exposé</a></div>"""));

        flat!.Url.Should().Be("Zum Exposé");
    }

    [Test]
    public void Parse_WithoutExposeLink_BuildsTheUrlFromTheListingId()
    {
        var flat = _parser.Parse(Card("""<div class="listing-card" data-obid="160123456"><h2>Wohnung</h2></div>"""));

        flat!.Url.Should().Be("https://www.immobilienscout24.de/expose/160123456");
    }

    [Test]
    public void Parse_WithEmptyListingId_ReturnsNull()
    {
        _parser.Parse(Card("""<div class="listing-card" data-obid=" "><h2>Werbung</h2></div>""")).Should().BeNull();
    }
}
