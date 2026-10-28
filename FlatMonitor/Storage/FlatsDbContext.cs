using FlatMonitor.Models;

namespace FlatMonitor.Storage;

using FlatMonitor.Models;
using Microsoft.EntityFrameworkCore;
using ScrapingHistory = ScrapingHistory;

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

        var snapshotBuilder = modelBuilder.Entity<FlatSnapshot>();

        snapshotBuilder.HasKey(s => new { s.Source, s.ListingId });
        snapshotBuilder.ToTable("FlatsSnapshots");

        snapshotBuilder.Property(s => s.Source)
            .IsRequired()
            .HasMaxLength(50);

        snapshotBuilder.Property(s => s.ListingId)
            .HasMaxLength(50);

        snapshotBuilder.Property(s => s.Url)
            .HasMaxLength(500);

        snapshotBuilder.Property(s => s.Address)
            .HasMaxLength(500);

        snapshotBuilder.Property(s => s.PostalCode)
            .HasMaxLength(10);

        snapshotBuilder.Property(s => s.District)
            .HasMaxLength(100);

        snapshotBuilder.Property(s => s.OriginalPriceEur);

        snapshotBuilder.Property(s => s.PricePerMeter);

        snapshotBuilder.Property(s => s.ScrapedAtUtc)
            .IsRequired()
            .HasMaxLength(50);

        // Owned type for Details — flattened into the OhneMaklerSnapshots table
        snapshotBuilder.OwnsOne(s => s.Details, detailsBuilder =>
        {
            detailsBuilder.Property(d => d.Title)
                .HasColumnName("Title")
                .HasMaxLength(500);

            detailsBuilder.Property(d => d.PropertyType)
                .HasColumnName("PropertyType")
                .HasMaxLength(50);

            detailsBuilder.Property(d => d.BuildYear)
                .HasColumnName("BuildYear");

            detailsBuilder.Property(d => d.Floor)
                .HasColumnName("Floor")
                .HasMaxLength(50);

            detailsBuilder.Property(d => d.Condition)
                .HasColumnName("Condition")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.Heating)
                .HasColumnName("Heating")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.ServiceChargeEur)
                .HasColumnName("ServiceChargeEur");

            detailsBuilder.Property(d => d.EnergyCertificateType)
                .HasColumnName("EnergyCertificateType")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.PrimaryEnergySource)
                .HasColumnName("PrimaryEnergySource")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.EnergyEfficiencyClass)
                .HasColumnName("EnergyEfficiencyClass")
                .HasMaxLength(10);

            detailsBuilder.Property(d => d.EnergyConsumption)
                .HasColumnName("EnergyConsumption")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.ListingStatus)
                .HasColumnName("ListingStatus")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.CurrentUse)
                .HasColumnName("CurrentUse")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.Availability)
                .HasColumnName("Availability")
                .HasMaxLength(100);

            detailsBuilder.Property(d => d.CommissionPercent)
                .HasColumnName("CommissionPercent");
        });

        // Indexes
        snapshotBuilder.HasIndex(s => new { s.Source, s.Url, s.Address })
            .IsUnique()
            .HasDatabaseName("idx_Source_Url_Address");

        snapshotBuilder.HasIndex(s => s.ScrapedAtUtc)
            .HasDatabaseName("idx_ScrapedAtUtc");

        snapshotBuilder.HasIndex(s => s.PostalCode)
            .HasDatabaseName("idx_PostalCode");

        snapshotBuilder.HasIndex(s => s.District)
            .HasDatabaseName("idx_District");

        // Audit columns
        snapshotBuilder.Property<string>("CreatedAtUtc")
            .IsRequired()
            .HasMaxLength(50);

        snapshotBuilder.Property<string>("UpdatedAtUtc")
            .IsRequired()
            .HasMaxLength(50);

        // ScrapingHistory configuration
        var historyBuilder = modelBuilder.Entity<ScrapingHistory>();
        historyBuilder.HasKey(h => h.Id);
        historyBuilder.ToTable("ScrapingHistories");
        historyBuilder.Property(h => h.StartTimeUtc).IsRequired();
        historyBuilder.Property(h => h.EndTimeUtc);
        historyBuilder.Property(h => h.Status).IsRequired().HasMaxLength(50);
        historyBuilder.Property(h => h.ErrorMessage).HasMaxLength(500);
        historyBuilder.Property(h => h.FlatsScrapedCount).IsRequired().HasDefaultValue(0);

        // FlatAggregationSnapshot configuration
        var aggregationBuilder = modelBuilder.Entity<FlatAggregationSnapshot>();
        aggregationBuilder.HasKey(a => a.Id);
        aggregationBuilder.ToTable("FlatAggregations");

        aggregationBuilder.Property(a => a.District)
            .HasMaxLength(100);

        aggregationBuilder.Property(a => a.RoomsCount);

        aggregationBuilder.Property(a => a.Count)
            .IsRequired();

        aggregationBuilder.Property(a => a.AvgPricePerMeter)
            .IsRequired()
            .HasColumnType("TEXT");

        aggregationBuilder.Property(a => a.ScrapingDate)
            .IsRequired();

        aggregationBuilder.HasIndex(a => new { a.District, a.RoomsCount })
            .HasDatabaseName("idx_FlatAggregations_District_RoomsCount");

        aggregationBuilder.HasIndex(a => a.ScrapingDate)
            .HasDatabaseName("idx_FlatAggregations_ScrapingDate");

        // FilterPreset configuration
        var presetBuilder = modelBuilder.Entity<FilterPreset>();
        presetBuilder.HasKey(p => p.Id);
        presetBuilder.ToTable("FilterPresets");

        presetBuilder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(FilterPreset.MaxNameLength)
            .UseCollation("NOCASE");

        // Primitive collection, stored as a JSON text column
        presetBuilder.PrimitiveCollection(p => p.Districts)
            .IsRequired();

        presetBuilder.Property(p => p.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        presetBuilder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        presetBuilder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("idx_FilterPresets_Name");

        // Partial unique index: at most one preset can be the default
        presetBuilder.HasIndex(p => p.IsDefault)
            .IsUnique()
            .HasFilter("\"IsDefault\" = 1")
            .HasDatabaseName("idx_FilterPresets_SingleDefault");
    }
}
