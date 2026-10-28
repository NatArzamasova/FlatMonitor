using NUnit.Framework;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using FlatMonitor.Storage;

namespace FlatMonitor.Tests.Storage;

[TestFixture]
public class StorageOptionsTests
{
    private string _contentRoot = null!;

    [SetUp]
    public void Setup()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"StorageOptionsTests_{Guid.NewGuid()}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    [Test]
    public void FromConfiguration_RelativePaths_AreResolvedAgainstContentRoot()
    {
        var storage = StorageOptions.FromConfiguration(Config("data", "Data Source=data/listings.db"), _contentRoot);

        storage.DataDirectory.Should().Be(Path.Combine(_contentRoot, "data"));
        storage.ConnectionString.Should().Be($"Data Source={Path.Combine(_contentRoot, "data", "listings.db")}");
    }

    [Test]
    public void FromConfiguration_AbsolutePaths_AreKept()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "elsewhere");
        var dbPath = Path.Combine(dataDir, "other.db");

        var storage = StorageOptions.FromConfiguration(Config(dataDir, $"Data Source={dbPath}"), _contentRoot);

        storage.DataDirectory.Should().Be(dataDir);
        storage.ConnectionString.Should().Be($"Data Source={dbPath}");
    }

    [Test]
    public void FromConfiguration_InMemoryDatabase_IsKept()
    {
        var storage = StorageOptions.FromConfiguration(Config("data", "Data Source=:memory:"), _contentRoot);

        storage.ConnectionString.Should().Be("Data Source=:memory:");
    }

    [Test]
    public void FromConfiguration_MissingDataDirectory_ThrowsWithHint()
    {
        var act = () => StorageOptions.FromConfiguration(Config(null, "Data Source=data/listings.db"), _contentRoot);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Storage:DataDirectory*appsettings.json*");
    }

    [Test]
    public void FromConfiguration_MissingConnectionString_ThrowsWithHint()
    {
        var act = () => StorageOptions.FromConfiguration(Config("data", null), _contentRoot);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*connection string 'Flats'*appsettings.json*");
    }

    [Test]
    public void EnsureDirectoriesExist_CreatesDataAndDatabaseDirectories()
    {
        var storage = StorageOptions.FromConfiguration(Config("data", "Data Source=db/listings.db"), _contentRoot);

        storage.EnsureDirectoriesExist();

        Directory.Exists(Path.Combine(_contentRoot, "data")).Should().BeTrue();
        Directory.Exists(Path.Combine(_contentRoot, "db")).Should().BeTrue();
    }

    private static IConfiguration Config(string? dataDirectory, string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [StorageOptions.DataDirectoryKey] = dataDirectory,
                [$"ConnectionStrings:{StorageOptions.ConnectionStringName}"] = connectionString,
            })
            .Build();
}
