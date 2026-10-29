using System.Reflection;
using System.Text.Json;
using FlatMonitor.Models;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests.Models;

[TestFixture]
public class FlatTests
{
    private static Flat CreateFlat(int? price = 300000, double? area = 75) => new()
    {
        Source = "ImmobilienScout24",
        ListingId = "1",
        Url = "https://example.org/1",
        Title = "Title",
        Address = "Address",
        PostalCode = "76199",
        District = "Rüppurr",
        PriceEur = price,
        OriginalPriceEur = 320000,
        RoomsCount = 3.5,
        LivingAreaSqm = area,
        CommissionPercent = 3.57,
        Details = new FlatDetails { Heating = "Gas" },
        ScrapedAtUtc = new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc),
    };

    [TestCase(300000, 75.0, 4000)]
    [TestCase(100000, 30.0, 3333)]
    [TestCase(100001, 30.0, 3333)]
    [TestCase(100010, 30.0, 3334)]
    public void PricePerMeter_IsPriceDividedByAreaRounded(int price, double area, int expected)
    {
        CreateFlat(price, area).PricePerMeter.Should().Be(expected);
    }

    [TestCase(null, 75.0)]
    [TestCase(300000, null)]
    [TestCase(300000, 0.0)]
    public void PricePerMeter_IsNullWithoutPriceOrPositiveArea(int? price, double? area)
    {
        CreateFlat(price, area).PricePerMeter.Should().BeNull();
    }

    [Test]
    public void PricePerMeter_FollowsPriceInWithExpressions()
    {
        (CreateFlat() with { PriceEur = 150000 }).PricePerMeter.Should().Be(2000);
    }

    [Test]
    public void Snapshot_From_CopiesEveryStoredField()
    {
        var flat = CreateFlat();

        var snapshot = FlatSnapshot.From(flat);

        snapshot.Should().BeEquivalentTo(new
        {
            flat.Source,
            flat.ListingId,
            flat.Url,
            flat.Title,
            flat.Address,
            flat.PostalCode,
            flat.District,
            flat.PriceEur,
            flat.OriginalPriceEur,
            PricePerMeter = 4000,
            flat.RoomsCount,
            flat.LivingAreaSqm,
            flat.CommissionPercent,
            flat.Details,
            flat.ScrapedAtUtc,
        });
    }

    [Test]
    public void Snapshot_From_LeavesNoSettablePropertyAtItsDefault()
    {
        // Guards against a new FlatSnapshot property that the mapping forgets
        var snapshot = FlatSnapshot.From(CreateFlat());

        var unset = typeof(FlatSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetValue(snapshot) is null || Equals(p.GetValue(snapshot), DefaultOf(p.PropertyType)))
            .Select(p => p.Name);

        unset.Should().BeEmpty();
    }

    [Test]
    public void Snapshot_UpdateFrom_ReplacesEveryField()
    {
        var snapshot = FlatSnapshot.From(CreateFlat());
        var updated = CreateFlat() with { Title = "New title", CommissionPercent = null, PriceEur = 150000, Details = null };

        snapshot.UpdateFrom(updated);

        snapshot.Title.Should().Be("New title");
        snapshot.CommissionPercent.Should().BeNull();
        snapshot.PricePerMeter.Should().Be(2000);
        snapshot.Details.Should().BeNull();
    }

    [Test]
    public void Snapshot_UpdateFrom_RejectsAFlatWithAnotherKey()
    {
        var snapshot = FlatSnapshot.From(CreateFlat());

        var act = () => snapshot.UpdateFrom(CreateFlat() with { ListingId = "2" });

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Snapshot_FromAStagedRow_CopiesEveryStoredField()
    {
        // Guards against a new FlatRecord property that publishing a staged row forgets
        var staged = StagedFlatSnapshot.From(CreateFlat());

        var snapshot = FlatSnapshot.From(staged);

        snapshot.Should().BeEquivalentTo(staged);
        typeof(FlatRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetValue(snapshot) is null || Equals(p.GetValue(snapshot), DefaultOf(p.PropertyType)))
            .Select(p => p.Name)
            .Should().BeEmpty();
    }

    [Test]
    public void Snapshot_UpdateFromAStagedRow_RejectsAnotherKey()
    {
        var snapshot = FlatSnapshot.From(CreateFlat());

        var act = () => snapshot.UpdateFrom(StagedFlatSnapshot.From(CreateFlat() with { ListingId = "2" }));

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void ScrapingStatus_SerializesByName()
    {
        var history = new ScrapingHistory { StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.PartialFailed };

        var json = JsonSerializer.Serialize(history, JsonSerializerOptions.Web);

        json.Should().Contain("\"status\":\"PartialFailed\"");
        JsonSerializer.Deserialize<ScrapingHistory>(json, JsonSerializerOptions.Web)!.Status.Should().Be(ScrapingStatus.PartialFailed);
    }

    private static object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
