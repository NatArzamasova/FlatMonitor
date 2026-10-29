using FlatMonitor.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatMonitor.Tests.Storage;

/// <summary>Guards the model configuration: it must match the last migration, or a change needs a new migration.</summary>
[TestFixture]
public class FlatsDbContextModelTests
{
    [Test]
    public async Task Model_MatchesTheLastMigration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = new FlatsDbContext(new DbContextOptionsBuilder<FlatsDbContext>().UseSqlite(connection).Options);

        context.Database.HasPendingModelChanges().Should().BeFalse("the model differs from FlatsDbContextModelSnapshot; add a migration");
    }
}
