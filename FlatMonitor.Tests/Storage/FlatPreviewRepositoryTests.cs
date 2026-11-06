using FlatMonitor.Models;
using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class FlatPreviewRepositoryTests
{
    private SqliteConnection _connection = null!;
    private FlatsDbContext _dbContext = null!;
    private FlatPreviewRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        // In-memory SQLite: the BLOB column and the key as in the real database
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _dbContext = NewContext();
        await _dbContext.Database.EnsureCreatedAsync();
        _repository = new FlatPreviewRepository(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private FlatsDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(_connection).Options);

    private static FlatPreview Preview(string listingId, string imageUrl, byte[] data, string source = "OhneMakler") => new()
    {
        Source = source,
        ListingId = listingId,
        ImageUrl = imageUrl,
        ContentType = "image/jpeg",
        Data = data,
        DownloadedAtUtc = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc),
    };

    [Test]
    public async Task SaveAsync_StoresThePreviewWithItsBytes()
    {
        await _repository.SaveAsync([Preview("1", "https://pictures.example/1.jpg", [0xFF, 0xD8, 0xFF])]);

        var stored = await _repository.GetAsync("OhneMakler", "1");
        stored.Should().NotBeNull();
        stored!.Data.Should().Equal(0xFF, 0xD8, 0xFF);
        stored.ContentType.Should().Be("image/jpeg");
        stored.ImageUrl.Should().Be("https://pictures.example/1.jpg");
        stored.DownloadedAtUtc.Should().Be(new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task SaveAsync_ReplacesAFlatsExistingPreview()
    {
        await _repository.SaveAsync([Preview("1", "https://pictures.example/old.jpg", [1])]);

        await _repository.SaveAsync([Preview("1", "https://pictures.example/new.jpg", [2, 2])]);

        await using var context = NewContext();
        var rows = await context.FlatPreviews.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ImageUrl.Should().Be("https://pictures.example/new.jpg");
        rows[0].Data.Should().Equal(2, 2);
    }

    [Test]
    public async Task SaveAsync_AfterSaving_NoLongerTracksThePreviews()
    {
        // The context is shared with the whole scrape: the bytes must not stay in memory
        await _repository.SaveAsync([Preview("1", "https://pictures.example/1.jpg", [1])]);
        await _repository.SaveAsync([Preview("1", "https://pictures.example/2.jpg", [2]), Preview("2", "https://pictures.example/3.jpg", [3])]);

        _dbContext.ChangeTracker.Entries<FlatPreview>().Should().BeEmpty();
    }

    // ==================== ARCHIVE ====================

    [Test]
    public async Task SaveAsync_ArchivesAFlatsFirstPhoto()
    {
        var preview = Preview("1", "https://pictures.example/1.jpg", [0xFF, 0xD8, 0xFF]);

        await _repository.SaveAsync([preview]);

        await using var context = NewContext();
        (await context.FlatPreviewsArchive.SingleAsync()).Should().BeEquivalentTo(preview);
    }

    [Test]
    public async Task SaveAsync_WithANewPhoto_ReplacesTheCurrentOneButKeepsTheArchivedOne()
    {
        await _repository.SaveAsync([Preview("1", "https://pictures.example/old.jpg", [1])]);

        await _repository.SaveAsync([Preview("1", "https://pictures.example/new.jpg", [2, 2])]);

        await using var context = NewContext();
        (await context.FlatPreviews.SingleAsync()).ImageUrl.Should().Be("https://pictures.example/new.jpg");
        var archived = await context.FlatPreviewsArchive.SingleAsync();
        archived.ImageUrl.Should().Be("https://pictures.example/old.jpg");
        archived.Data.Should().Equal(1);
    }

    [Test]
    public async Task SaveAsync_AFlatTwiceInOneBatch_ArchivesItsFirstPhotoOnce()
    {
        await _repository.SaveAsync([Preview("1", "https://pictures.example/a.jpg", [1]), Preview("1", "https://pictures.example/b.jpg", [2])]);

        await using var context = NewContext();
        (await context.FlatPreviewsArchive.SingleAsync()).ImageUrl.Should().Be("https://pictures.example/a.jpg");
        (await context.FlatPreviews.SingleAsync()).ImageUrl.Should().Be("https://pictures.example/b.jpg");
    }

    [Test]
    public async Task SaveAsync_ForAFlatThatCameBack_StoresItsPhotoAgainAndKeepsTheArchivedOne()
    {
        // Arrange: the flat was removed, so its current preview was deleted; its archived photo stayed
        await _repository.SaveAsync([Preview("1", "https://pictures.example/old.jpg", [1])]);
        await _dbContext.FlatPreviews.ExecuteDeleteAsync();

        // Act
        await _repository.SaveAsync([Preview("1", "https://pictures.example/new.jpg", [2])]);

        // Assert
        await using var context = NewContext();
        (await context.FlatPreviews.SingleAsync()).ImageUrl.Should().Be("https://pictures.example/new.jpg");
        (await context.FlatPreviewsArchive.SingleAsync()).ImageUrl.Should().Be("https://pictures.example/old.jpg");
    }

    [Test]
    public async Task SaveAsync_ArchivesPerSource()
    {
        await _repository.SaveAsync([Preview("1", "https://pictures.example/om-1.jpg", [1])]);

        await _repository.SaveAsync([Preview("1", "https://pictures.example/is24-1.jpg", [2], source: "ImmobilienScout24")]);

        await using var context = NewContext();
        (await context.FlatPreviewsArchive.OrderBy(p => p.Source).Select(p => p.Source + "/" + p.ImageUrl).ToListAsync())
            .Should().Equal("ImmobilienScout24/https://pictures.example/is24-1.jpg", "OhneMakler/https://pictures.example/om-1.jpg");
    }

    [Test]
    public async Task SaveAsync_AfterSaving_NoLongerTracksTheArchivedPhotos()
    {
        await _repository.SaveAsync([Preview("1", "https://pictures.example/1.jpg", [1])]);
        await _repository.SaveAsync([Preview("1", "https://pictures.example/2.jpg", [2]), Preview("2", "https://pictures.example/3.jpg", [3])]);

        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_WhenTheSaveFails_ArchivesNothingAndTracksNothing()
    {
        // Arrange: a preview without bytes violates the NOT NULL column
        var broken = Preview("2", "https://pictures.example/2.jpg", [2]);
        broken.Data = null!;

        // Act
        var save = () => _repository.SaveAsync([Preview("1", "https://pictures.example/1.jpg", [1]), broken]);

        // Assert
        await save.Should().ThrowAsync<DbUpdateException>();
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
        await using var context = NewContext();
        (await context.FlatPreviewsArchive.AnyAsync()).Should().BeFalse();
        (await context.FlatPreviews.AnyAsync()).Should().BeFalse();
    }

    [Test]
    public async Task GetImageUrlsAsync_ReturnsTheStoredUrlsOfTheRequestedFlatsOfTheSource()
    {
        await _repository.SaveAsync([
            Preview("1", "https://pictures.example/1.jpg", [1]),
            Preview("2", "https://pictures.example/2.jpg", [2]),
            Preview("1", "https://pictures.example/is24-1.jpg", [3], source: "ImmobilienScout24"),
        ]);

        var urls = await _repository.GetImageUrlsAsync("OhneMakler", ["1", "3"]);

        urls.Should().Equal(new Dictionary<string, string> { ["1"] = "https://pictures.example/1.jpg" });
    }

    [Test]
    public async Task GetAsync_WithoutPreview_IsNull()
    {
        (await _repository.GetAsync("OhneMakler", "missing")).Should().BeNull();
    }
}
