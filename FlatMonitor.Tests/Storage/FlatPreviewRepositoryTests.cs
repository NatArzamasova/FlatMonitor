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
