namespace workInProgress.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using workInProgress.Storage;

public class FlatsDbContextFactory : IDesignTimeDbContextFactory<FlatsDbContext>
{
    public FlatsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FlatsDbContext>();
        var dataDir = StoragePaths.GetDefaultDataDirectory();
        var dbPath = Path.Combine(dataDir, "listings.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new FlatsDbContext(optionsBuilder.Options);
    }
}
