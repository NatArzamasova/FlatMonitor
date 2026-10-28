namespace FlatMonitor.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Used by <c>dotnet ef</c>, which runs with the FlatMonitor project directory as working directory,
/// so it reads the same appsettings.json and database as the app.
/// </summary>
public class FlatsDbContextFactory : IDesignTimeDbContextFactory<FlatsDbContext>
{
    public FlatsDbContext CreateDbContext(string[] args)
    {
        var contentRoot = Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var storage = StorageOptions.FromConfiguration(configuration, contentRoot);

        var optionsBuilder = new DbContextOptionsBuilder<FlatsDbContext>();
        optionsBuilder.UseSqlite(storage.ConnectionString);
        return new FlatsDbContext(optionsBuilder.Options);
    }
}
