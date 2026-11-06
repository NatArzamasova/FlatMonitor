using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

/// <summary>The archive tables' keys and indexes, and that the other flats tables kept theirs.</summary>
[TestFixture]
public class FlatsArchiveModelTests
{
    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);
        await _dbContext.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private IEntityType Entity<T>() => _dbContext.Model.FindEntityType(typeof(T))!;

    private static string[] KeyOf(IEntityType entity) => entity.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray();

    [Test]
    public void FlatsArchive_IsKeyedByFlatAndRun()
    {
        var archive = Entity<ArchivedFlat>();

        archive.GetTableName().Should().Be("FlatsArchive");
        KeyOf(archive).Should().Equal("Source", "ListingId", "ScrapingHistoryId");
    }

    [Test]
    public void FlatsArchive_HasNoUniqueIndex_ButAnIndexOnTheRun()
    {
        var indexes = Entity<ArchivedFlat>().GetIndexes().ToList();

        indexes.Should().NotContain(i => i.IsUnique);
        indexes.Should().ContainSingle().Which.GetDatabaseName().Should().Be("idx_Archive_ScrapingHistoryId");
    }

    [Test]
    public void FlatsArchive_ReferencesItsRun_WithoutCascadeDelete()
    {
        var foreignKey = Entity<ArchivedFlat>().GetForeignKeys().Should().ContainSingle().Subject;

        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(ScrapingHistory));
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Test]
    public void FlatsArchive_HasTheColumnsOfFlatsSnapshots_PlusTheRun()
    {
        static IEnumerable<string> Columns(IEntityType entity) =>
            entity.GetProperties()
                .Concat(entity.GetNavigations().Where(n => n.TargetEntityType.IsOwned())
                    .SelectMany(n => n.TargetEntityType.GetProperties().Where(p => !p.IsPrimaryKey())))
                .Select(p => p.GetColumnName());

        Columns(Entity<ArchivedFlat>()).Should().BeEquivalentTo(Columns(Entity<PublishedFlat>()).Append("ScrapingHistoryId"));
    }

    [Test]
    public void FlatPreviewsArchive_IsKeyedLikeFlatPreviews()
    {
        var archive = Entity<ArchivedFlatPreview>();

        archive.GetTableName().Should().Be("FlatPreviewsArchive");
        KeyOf(archive).Should().Equal("Source", "ListingId");
        archive.GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(Entity<FlatPreview>().GetProperties().Select(p => p.Name));
    }

    [Test]
    public void FlatsSnapshotsAndStaging_KeepTheirKeyAndUniqueIndex()
    {
        foreach (var (entity, uniqueIndex) in new[]
                 {
                     (Entity<PublishedFlat>(), "idx_Source_Url_Address"),
                     (Entity<StagedFlat>(), "idx_Staging_Source_Url_Address"),
                 })
        {
            KeyOf(entity).Should().Equal("Source", "ListingId");
            entity.GetIndexes().Should().ContainSingle(i => i.IsUnique)
                .Which.GetDatabaseName().Should().Be(uniqueIndex);
        }
    }

    [Test]
    public async Task FlatsArchive_KeepsOneRowPerRun_OfTheSameFlat()
    {
        _dbContext.ScrapingHistories.AddRange(
            new ScrapingHistory { Id = 1, StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.Successful },
            new ScrapingHistory { Id = 2, StartTimeUtc = DateTime.UtcNow, Status = ScrapingStatus.Successful });
        var flat = PublishedFlat.From(new Flat { Source = "OhneMakler", ListingId = "42", Url = "https://example.org/42", ScrapedAtUtc = DateTime.UtcNow });
        foreach (var runId in new[] { 1, 2 })
        {
            var archived = ArchivedFlat.From(flat, runId);
            _dbContext.FlatsArchive.Add(archived);
            _dbContext.Entry(archived).Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow;
            _dbContext.Entry(archived).Property("UpdatedAtUtc").CurrentValue = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        (await _dbContext.FlatsArchive.CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task FlatsArchive_RejectsARowOfAnUnknownRun()
    {
        var archived = ArchivedFlat.From(
            PublishedFlat.From(new Flat { Source = "OhneMakler", ListingId = "42", ScrapedAtUtc = DateTime.UtcNow }), scrapingHistoryId: 99);
        _dbContext.FlatsArchive.Add(archived);
        _dbContext.Entry(archived).Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow;
        _dbContext.Entry(archived).Property("UpdatedAtUtc").CurrentValue = DateTime.UtcNow;

        var save = () => _dbContext.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }
}
