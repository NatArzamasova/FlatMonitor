namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;

public class FlatsDbContext : DbContext
{
    public DbSet<FlatSnapshot> FlatsSnapshots { get; set; }
    public DbSet<ScrapingHistory> ScrapingHistories { get; set; }
    public DbSet<FlatAggregationSnapshot> FlatAggregations { get; set; }
    public DbSet<FilterPreset> FilterPresets { get; set; }

    public FlatsDbContext(DbContextOptions<FlatsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // One IEntityTypeConfiguration<T> per entity, in Storage/Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FlatsDbContext).Assembly);
    }
}
