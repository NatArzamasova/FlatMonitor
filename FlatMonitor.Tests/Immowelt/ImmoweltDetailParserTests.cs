namespace FlatMonitor.Tests.Immowelt;

using System.Text.Json;
using FlatMonitor.Models;
using FlatMonitor.Scraping.Immowelt;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class ImmoweltDetailParserTests
{
    private ImmoweltDetailParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ImmoweltDetailParser();
    }

    private ImmoweltDetailPage ParseFixture() => _parser.Parse(ImmoweltFixtures.Read("expose-page.html"));

    /// <summary>An expose page whose data script holds <paramref name="classifiedJson"/>, escaped as immowelt does.</summary>
    private static string ExposeHtml(string classifiedJson)
    {
        var json = """{"app_demand_referral_cldp":{"data":{"classified":""" + classifiedJson + ""","error":null}}}""";
        return $"<script>window[\"__UFRN_LIFECYCLE_SERVERREQUEST__\"]=JSON.parse({JsonSerializer.Serialize(json)});</script>";
    }

    private static Flat Card() => new()
    {
        Source = "Immowelt",
        ListingId = "25DJIC4KVE5U",
        Url = "https://www.immowelt.de/expose/0294187d-a3bf-49a2-990b-eb64ace539ed",
        Title = "Card title",
        Address = "76189 Karlsruhe",
        PostalCode = "76189",
        District = "Oberreut",
        PriceEur = 310000,
        OriginalPriceEur = 310000,
        RoomsCount = 3,
        LivingAreaSqm = 75.04,
        ScrapedAtUtc = DateTime.UtcNow,
    };

    [Test]
    public void Parse_Expose_ReadsTheFlatsValues()
    {
        var page = ParseFixture();

        page.Title.Should().Be("Gefragte Wohnung mit Ausblick");
        page.Address.Should().Be("76189 Karlsruhe");
        page.PostalCode.Should().Be("76189");
        page.District.Should().Be("Oberreut");
        page.PriceEur.Should().Be(305000);
        page.RoomsCount.Should().Be(3);
        page.LivingAreaSqm.Should().Be(75);
        page.CommissionPercent.Should().Be(3.57);
        page.IsDeactivated.Should().BeFalse();
    }

    [Test]
    public void Parse_Expose_ReadsTheDetails()
    {
        var details = ParseFixture().Details!;

        details.PropertyType.Should().Be("Wohnung");
        details.BuildYear.Should().Be(1995);
        details.Floor.Should().Be("1. Geschoss");
        details.Condition.Should().Be("Neubau, neuwertig");
        details.Heating.Should().Be("Zentralheizung");
        details.ServiceChargeEur.Should().Be(346);
        details.EnergyCertificateType.Should().Be("Verbrauchsausweis");
        details.PrimaryEnergySource.Should().Be("Erdgas H");
        details.EnergyEfficiencyClass.Should().Be("A");
        details.EnergyConsumption.Should().Be("40,8 kWh/(m²·a)");
    }

    [Test]
    public void Parse_PageWithoutDataScript_HasNoValues()
    {
        var page = _parser.Parse("""<html><body><p id="cmsg">Please enable JS and disable any ad blocker</p></body></html>""");

        page.Title.Should().BeNull();
        page.PriceEur.Should().BeNull();
        page.CommissionPercent.Should().BeNull();
        page.Details.Should().BeNull();
        page.IsDeactivated.Should().BeFalse();
    }

    [Test]
    public void Parse_CommissionFree_IsZero()
    {
        var html = ExposeHtml("""{"sections":{"price":{"base":{"commissionFee":{"label":"Provision für Käufer","value":"provisionsfrei"}}}}}""");

        _parser.Parse(html).CommissionPercent.Should().Be(0);
    }

    [Test]
    public void Parse_NoCommissionFee_IsUnknown()
    {
        var html = ExposeHtml("""{"sections":{"price":{"base":{"main":{"value":{"main":{"value":"305.000 €"}}}}}}}""");

        var page = _parser.Parse(html);

        page.CommissionPercent.Should().BeNull();
        page.PriceEur.Should().Be(305000);
    }

    [Test]
    public void Parse_UnpublishedListing_IsDeactivated()
    {
        var html = ExposeHtml("""{"metadata":{"status":{"status":false}},"sections":{}}""");

        _parser.Parse(html).IsDeactivated.Should().BeTrue();
    }

    [Test]
    public void Parse_EmptySections_HasEmptyDetails()
    {
        var details = _parser.Parse(ExposeHtml("""{"sections":{}}""")).Details!;

        details.BuildYear.Should().BeNull();
        details.Heating.Should().BeNull();
        details.EnergyEfficiencyClass.Should().BeNull();
        details.ServiceChargeEur.Should().BeNull();
    }

    [Test]
    public void ApplyTo_PageValuesReplaceTheCards_ExceptTheExactRoomsAndArea()
    {
        var flat = ParseFixture().ApplyTo(Card());

        flat.Title.Should().Be("Gefragte Wohnung mit Ausblick");
        flat.PriceEur.Should().Be(305000);
        flat.OriginalPriceEur.Should().Be(305000, "the card had no previous price");
        flat.CommissionPercent.Should().Be(3.57);
        flat.Details!.BuildYear.Should().Be(1995);
        flat.LivingAreaSqm.Should().Be(75.04);
        flat.RoomsCount.Should().Be(3);
        flat.ListingId.Should().Be("25DJIC4KVE5U");
        flat.Url.Should().Be(Card().Url);
    }

    [Test]
    public void ApplyTo_CardWithPreviousPrice_KeepsItAsOriginal()
    {
        var card = Card() with { PriceEur = 305000, OriginalPriceEur = 329000 };

        ParseFixture().ApplyTo(card).OriginalPriceEur.Should().Be(329000);
    }

    [Test]
    public void ApplyTo_PageWithoutValues_KeepsTheCard()
    {
        var card = Card();

        var flat = _parser.Parse("<html></html>").ApplyTo(card);

        flat.Should().BeEquivalentTo(card with { Details = null });
    }
}
