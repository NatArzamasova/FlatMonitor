namespace workInProgress.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using workInProgress.Storage;

public class ListingsDbContextFactory : IDesignTimeDbContextFactory<ListingsDbContext>
{
    public ListingsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ListingsDbContext>();
        var dataDir = StoragePaths.GetDefaultDataDirectory();
        var dbPath = Path.Combine(dataDir, "listings.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new ListingsDbContext(optionsBuilder.Options);
    }
}
