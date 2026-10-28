using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

// Uses SQLite, not InMemory, so the JSON column, NOCASE collation and partial unique index are exercised.
[TestFixture]
public class FilterPresetMappingTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<FlatsDbContext> _options = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var context = new FlatsDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown() => await _connection.DisposeAsync();

    [Test]
    public async Task FilterPreset_RoundTripsAllProperties()
    {
        var updatedAt = new DateTime(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);
        await AddAsync(new FilterPreset
        {
            Name = "South",
            Districts = ["Durlach", "Grötzingen", "Südstadt"],
            IsDefault = true,
            UpdatedAtUtc = updatedAt
        });

        await using var readContext = new FlatsDbContext(_options);
        var stored = await readContext.FilterPresets.SingleAsync();

        stored.Id.Should().BePositive();
        stored.Name.Should().Be("South");
        stored.Districts.Should().Equal("Durlach", "Grötzingen", "Südstadt");
        stored.IsDefault.Should().BeTrue();
        stored.UpdatedAtUtc.Should().Be(updatedAt);
    }

    [Test]
    public async Task FilterPreset_RoundTripsEmptyDistricts()
    {
        await AddAsync(Preset("All"));

        await using var readContext = new FlatsDbContext(_options);
        var stored = await readContext.FilterPresets.SingleAsync();

        stored.Districts.Should().BeEmpty();
        stored.IsDefault.Should().BeFalse();
    }

    [Test]
    public async Task FilterPreset_StoresDistrictsAsJsonText()
    {
        await AddAsync(Preset("East", "Durlach"));

        var raw = await ScalarAsync("SELECT Districts FROM FilterPresets");

        raw.Should().Be("[\"Durlach\"]");
    }

    [Test]
    public async Task FilterPreset_AllowsMultiplePresetsWithDifferentNames()
    {
        await AddAsync(Preset("East", "Durlach"));
        await AddAsync(Preset("West", "Weststadt"));

        await using var readContext = new FlatsDbContext(_options);
        var names = await readContext.FilterPresets.Select(p => p.Name).ToListAsync();

        names.Should().BeEquivalentTo("East", "West");
    }

    [TestCase("East")]
    [TestCase("EAST")]
    public async Task FilterPreset_RejectsDuplicateNameIgnoringCase(string duplicateName)
    {
        await AddAsync(Preset("East"));

        var act = () => AddAsync(Preset(duplicateName));

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task FilterPreset_FindsByNameIgnoringCase()
    {
        await AddAsync(Preset("East"));

        await using var readContext = new FlatsDbContext(_options);
        var found = await readContext.FilterPresets.SingleOrDefaultAsync(p => p.Name == "east");

        found.Should().NotBeNull();
    }

    [Test]
    public async Task FilterPreset_AllowsManyNonDefaultPresets()
    {
        await AddAsync(Preset("East"));
        await AddAsync(Preset("West"));
        await AddAsync(Preset("North", isDefault: true));

        await using var readContext = new FlatsDbContext(_options);
        (await readContext.FilterPresets.CountAsync()).Should().Be(3);
    }

    [Test]
    public async Task FilterPreset_RejectsSecondDefault()
    {
        await AddAsync(Preset("East", isDefault: true));

        var act = () => AddAsync(Preset("West", isDefault: true));

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static FilterPreset Preset(string name, string? district = null, bool isDefault = false) => new()
    {
        Name = name,
        Districts = district is null ? [] : [district],
        IsDefault = isDefault,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private async Task AddAsync(FilterPreset preset)
    {
        await using var context = new FlatsDbContext(_options);
        context.FilterPresets.Add(preset);
        await context.SaveChangesAsync();
    }

    private async Task<string?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (string?)await command.ExecuteScalarAsync();
    }
}
