using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

// Uses SQLite, not InMemory: SetDefaultAsync needs transactions and the partial unique index.
[TestFixture]
public class FilterPresetRepositoryTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<FlatsDbContext> _options = null!;
    private FlatsDbContext _context = null!;
    private FilterPresetRepository _repository = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<FlatsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new FlatsDbContext(_options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new FilterPresetRepository(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ==================== READ ====================

    [Test]
    public async Task GetAllAsync_ReturnsEmptyListWhenThereAreNoPresets()
    {
        var result = await _repository.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetAllAsync_ReturnsPresetsOrderedByName()
    {
        await _repository.CreateAsync("West", []);
        await _repository.CreateAsync("east", []);
        await _repository.CreateAsync("North", []);

        var result = await _repository.GetAllAsync();

        result.Select(p => p.Name).Should().Equal("east", "North", "West");
    }

    [Test]
    public async Task GetByIdAsync_ReturnsPreset()
    {
        var created = await _repository.CreateAsync("East", ["Durlach"]);

        var result = await _repository.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("East");
        result.Districts.Should().Equal("Durlach");
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNullForUnknownId()
    {
        var result = await _repository.GetByIdAsync(42);

        result.Should().BeNull();
    }

    // ==================== NAME EXISTS ====================

    [TestCase("East")]
    [TestCase("east")]
    [TestCase("EAST")]
    public async Task NameExistsAsync_IgnoresCase(string name)
    {
        await _repository.CreateAsync("East", []);

        var result = await _repository.NameExistsAsync(name);

        result.Should().BeTrue();
    }

    [Test]
    public async Task NameExistsAsync_ReturnsFalseForUnknownName()
    {
        await _repository.CreateAsync("East", []);

        var result = await _repository.NameExistsAsync("West");

        result.Should().BeFalse();
    }

    [Test]
    public async Task NameExistsAsync_SkipsExcludedPreset()
    {
        var east = await _repository.CreateAsync("East", []);

        var result = await _repository.NameExistsAsync("east", excludingId: east.Id);

        result.Should().BeFalse();
    }

    [Test]
    public async Task NameExistsAsync_FindsOtherPresetEvenWithExclusion()
    {
        var east = await _repository.CreateAsync("East", []);
        await _repository.CreateAsync("West", []);

        var result = await _repository.NameExistsAsync("West", excludingId: east.Id);

        result.Should().BeTrue();
    }

    // ==================== CREATE / UPDATE / DELETE ====================

    [Test]
    public async Task CreateAsync_StoresPresetAsNonDefault()
    {
        var before = DateTime.UtcNow;

        var created = await _repository.CreateAsync("East", ["Durlach", "Grötzingen"]);

        created.Id.Should().BePositive();
        var stored = await ReadAsync(created.Id);
        stored.Name.Should().Be("East");
        stored.Districts.Should().Equal("Durlach", "Grötzingen");
        stored.IsDefault.Should().BeFalse();
        stored.UpdatedAtUtc.Should().BeOnOrAfter(before);
    }

    [Test]
    public async Task CreateAsync_ThrowsOnDuplicateName()
    {
        await _repository.CreateAsync("East", []);

        var act = () => _repository.CreateAsync("EAST", []);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task UpdateAsync_ChangesNameAndDistricts()
    {
        var created = await _repository.CreateAsync("East", ["Durlach"]);
        var createdAt = created.UpdatedAtUtc;

        var updated = await _repository.UpdateAsync(created.Id, "Far east", ["Grötzingen", "Hagsfeld"]);

        updated.Should().NotBeNull();
        var stored = await ReadAsync(created.Id);
        stored.Name.Should().Be("Far east");
        stored.Districts.Should().Equal("Grötzingen", "Hagsfeld");
        stored.UpdatedAtUtc.Should().BeOnOrAfter(createdAt);
    }

    [Test]
    public async Task UpdateAsync_KeepsDefaultFlag()
    {
        var created = await _repository.CreateAsync("East", []);
        await _repository.SetDefaultAsync(created.Id);

        await _repository.UpdateAsync(created.Id, "East", ["Durlach"]);

        (await ReadAsync(created.Id)).IsDefault.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_ReturnsNullForUnknownId()
    {
        var result = await _repository.UpdateAsync(42, "East", []);

        result.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_RemovesPreset()
    {
        var east = await _repository.CreateAsync("East", []);
        var west = await _repository.CreateAsync("West", []);

        var result = await _repository.DeleteAsync(east.Id);

        result.Should().BeTrue();
        await using var readContext = new FlatsDbContext(_options);
        (await readContext.FilterPresets.Select(p => p.Id).ToListAsync()).Should().Equal(west.Id);
    }

    [Test]
    public async Task DeleteAsync_ReturnsFalseForUnknownId()
    {
        var result = await _repository.DeleteAsync(42);

        result.Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_OfDefaultLeavesNoDefault()
    {
        var east = await _repository.CreateAsync("East", []);
        await _repository.CreateAsync("West", []);
        await _repository.SetDefaultAsync(east.Id);

        await _repository.DeleteAsync(east.Id);

        (await DefaultIdsAsync()).Should().BeEmpty();
    }

    // ==================== DEFAULT ====================

    [Test]
    public async Task SetDefaultAsync_MarksPresetAsDefault()
    {
        var east = await _repository.CreateAsync("East", []);

        var result = await _repository.SetDefaultAsync(east.Id);

        result.Should().NotBeNull();
        result!.IsDefault.Should().BeTrue();
        (await DefaultIdsAsync()).Should().Equal(east.Id);
    }

    [Test]
    public async Task SetDefaultAsync_MovesDefaultToOtherPreset()
    {
        var east = await _repository.CreateAsync("East", []);
        var west = await _repository.CreateAsync("West", []);
        await _repository.SetDefaultAsync(east.Id);

        await _repository.SetDefaultAsync(west.Id);

        (await DefaultIdsAsync()).Should().Equal(west.Id);
    }

    [Test]
    public async Task SetDefaultAsync_OnCurrentDefaultIsNoOp()
    {
        var east = await _repository.CreateAsync("East", []);
        await _repository.SetDefaultAsync(east.Id);
        var updatedAt = (await ReadAsync(east.Id)).UpdatedAtUtc;

        var result = await _repository.SetDefaultAsync(east.Id);

        result!.IsDefault.Should().BeTrue();
        (await DefaultIdsAsync()).Should().Equal(east.Id);
        (await ReadAsync(east.Id)).UpdatedAtUtc.Should().Be(updatedAt);
    }

    [Test]
    public async Task SetDefaultAsync_ReturnsNullForUnknownIdAndKeepsCurrentDefault()
    {
        var east = await _repository.CreateAsync("East", []);
        await _repository.SetDefaultAsync(east.Id);

        var result = await _repository.SetDefaultAsync(42);

        result.Should().BeNull();
        (await DefaultIdsAsync()).Should().Equal(east.Id);
    }

    [Test]
    public async Task ClearDefaultAsync_LeavesNoDefault()
    {
        var east = await _repository.CreateAsync("East", []);
        await _repository.SetDefaultAsync(east.Id);

        await _repository.ClearDefaultAsync();

        (await DefaultIdsAsync()).Should().BeEmpty();
        (await ReadAsync(east.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task ClearDefaultAsync_WithoutDefaultDoesNothing()
    {
        await _repository.CreateAsync("East", []);

        var act = () => _repository.ClearDefaultAsync();

        await act.Should().NotThrowAsync();
        (await DefaultIdsAsync()).Should().BeEmpty();
    }

    // Reads through a fresh context, so results come from the database, not the change tracker.
    private async Task<FilterPreset> ReadAsync(int id)
    {
        await using var readContext = new FlatsDbContext(_options);
        return await readContext.FilterPresets.SingleAsync(p => p.Id == id);
    }

    private async Task<List<int>> DefaultIdsAsync()
    {
        await using var readContext = new FlatsDbContext(_options);
        return await readContext.FilterPresets.Where(p => p.IsDefault).Select(p => p.Id).ToListAsync();
    }
}
