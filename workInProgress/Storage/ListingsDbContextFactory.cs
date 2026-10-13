namespace workInProgress.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class ListingsDbContextFactory : IDesignTimeDbContextFactory<ListingsDbContext>
{
    public ListingsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ListingsDbContext>();
        optionsBuilder.UseSqlite("Data Source=data/listings.db");

        return new ListingsDbContext(optionsBuilder.Options);
    }
}
